import json
import time
import argparse
import requests
import os
import subprocess
import signal
import zipfile
import sys
import threading
from pathlib import Path
from typing import Dict, Any, List

def load_dotenv():
    env_path = Path(".env")
    if env_path.exists():
        for line in env_path.read_text().splitlines():
            if "=" in line and not line.startswith("#"):
                key, value = line.split("=", 1)
                os.environ[key.strip()] = value.strip()

class ShirariumBench:
    def __init__(self, port: int = 8080):
        self.port = port
        self.api_url = f"http://localhost:{port}"
        self.models_dir = Path("benchmarks/models")
        self.reports_dir = Path("benchmarks/reports")
        self.bin_dir = Path("benchmarks/bin")
        self.models_dir.mkdir(exist_ok=True)
        self.reports_dir.mkdir(exist_ok=True)
        self.bin_dir.mkdir(exist_ok=True)
        self.server_logs = []

    def ensure_llama_server(self) -> str:
        binary_name = "llama-server.exe" if os.name == 'nt' else "llama-server"
        local_binary = self.bin_dir / binary_name
        if local_binary.exists(): return str(local_binary)
        try:
            subprocess.check_call([binary_name, "--version"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            return binary_name
        except: pass

        print(f"INFO: llama-server not found. Downloading...")
        base_url = "https://github.com/ggml-org/llama.cpp/releases/download/b5092/"
        if os.name == 'nt':
            url = base_url + "llama-b5092-bin-win-vulkan-x64.zip"
            zip_path = self.bin_dir / "llama.zip"
            subprocess.check_call(["curl", "-L", url, "-o", str(zip_path)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            with zipfile.ZipFile(zip_path, 'r') as zip_ref:
                zip_ref.extractall(self.bin_dir)
            os.remove(zip_path)
            for p in self.bin_dir.rglob("llama-server.exe"): return str(p)
        return binary_name

    def download_model(self, model_info: Dict[str, Any]):
        target = self.models_dir / model_info["filename"]
        if target.exists(): return target
        print(f"INFO: Downloading {model_info['name']}...")
        hf_token = os.environ.get("HF_TOKEN")
        cmd = ["curl", "-L", "--progress-bar", model_info["url"], "-o", str(target)]
        if hf_token: cmd.extend(["-H", f"Authorization: Bearer {hf_token}"])
        subprocess.run(cmd)
        return target

    def start_server(self, model_path: Path, binary_path: str):
        cmd = [binary_path, "-m", str(model_path), "--port", str(self.port), "--n-gpu-layers", "0", "--ctx-size", "2048"]
        self.server_logs = []
        process = subprocess.Popen(
            cmd, 
            stdout=subprocess.PIPE, 
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0
        )
        
        def log_reader(proc):
            for line in iter(proc.stdout.readline, ""):
                self.server_logs.append(line.strip())
        
        threading.Thread(target=log_reader, args=(process,), daemon=True).start()

        for i in range(60):
            if process.poll() is not None:
                raise Exception(f"Server exited with code {process.poll()}. Logs:\n" + "\n".join(self.server_logs[-10:]))
            try:
                if requests.get(f"{self.api_url}/health", timeout=1).status_code == 200:
                    return process
            except: pass
            time.sleep(2)
        
        process.kill()
        raise Exception("Server timeout. Logs:\n" + "\n".join(self.server_logs[-20:]))

    def parse_with_llm(self, filename: str) -> Dict[str, Any]:
        system_prompt = (
            "You are a deterministic data extraction engine. Parse the provided media filename into a strict JSON object.\n"
            "Rules:\n"
            "- release_year: choose the release year (often in parentheses or near resolution).\n"
            "- title: the name of the media, including title years if applicable.\n"
            "- Use null for missing values. No conversational text."
        )

        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": "1984 (1984) 1080p BluRay.mkv"},
            {"role": "assistant", "content": '{"Title": "1984", "Year": 1984, "Resolution": "1080p", "Season": null, "Episode": null}'},
            {"role": "user", "content": "2012.2009.1080p.mkv"},
            {"role": "assistant", "content": '{"Title": "2012", "Year": 2009, "Resolution": "1080p", "Season": null, "Episode": null}'},
            {"role": "user", "content": f"Parse: {filename}"}
        ]
        schema = {
            "type": "object",
            "properties": {
                "Title": {"type": "string"}, "Year": {"type": ["number", "null"]},
                "Season": {"type": ["number", "null"]}, "Episode": {"type": ["number", "null"]},
                "Resolution": {"type": ["string", "null"]}
            },
            "required": ["Title", "Year", "Season", "Episode", "Resolution"]
        }

        start_time = time.perf_counter()
        try:
            response = requests.post(
                f"{self.api_url}/v1/chat/completions",
                json={"messages": messages, "temperature": 0.0, "response_format": {"type": "json_object", "schema": schema}},
                timeout=60
            )
            result_text = response.json()["choices"][0]["message"]["content"]
        except Exception as e:
            result_text = json.dumps({"error": str(e)})

        latency = (time.perf_counter() - start_time) * 1000
        try: parsed = json.loads(result_text)
        except: parsed = {"error": "Invalid JSON"}
        return parsed, latency

    def calculate_score(self, expected: Dict[str, Any], actual: Dict[str, Any]) -> float:
        fields = ["Title", "Year", "Season", "Episode", "Resolution"]
        correct = 0
        for f in fields:
            if str(expected.get(f) or expected.get(f.lower())).lower().strip() == \
               str(actual.get(f) or actual.get(f.lower())).lower().strip():
                correct += 1
        return correct / len(fields)

    def run_benchmark(self, model_info: Dict[str, Any], dataset_path: str, limit: int = 0):
        print(f"\n>>> Model: {model_info['name']}")
        binary_path = self.ensure_llama_server()
        model_path = self.download_model(model_info)
        server_process = self.start_server(model_path, binary_path)
        
        try:
            with open(dataset_path, 'r', encoding='utf-8') as f:
                items = json.load(f).get("entries", [])
            if limit > 0: items = items[:limit]
            
            total_lat, total_acc = 0, 0
            for idx, item in enumerate(items):
                filename = os.path.basename(item.get("relativePath", ""))
                expected = item.get("expected") or item
                actual, lat = self.parse_with_llm(filename)
                acc = self.calculate_score(expected, actual)
                
                # Single-line, clean feedback
                print(f"  [{idx+1}/{len(items)}] {acc*100:>3.0f}% | {lat:>5.0f}ms | {filename[:40]}", end="\r")
                
                total_lat += lat
                total_acc += acc
                
            avg_lat, avg_acc = total_lat / len(items), total_acc / len(items)
            print(f"\n  Final: Acc={avg_acc*100:.1f}%, Latency={avg_lat:.0f}ms")
            return {"id": model_info["id"], "name": model_info["name"], "acc": avg_acc, "lat": avg_lat}
        finally:
            subprocess.call(['taskkill', '/F', '/T', '/PID', str(server_process.pid)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

if __name__ == "__main__":
    load_dotenv()
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", default="datasets/regression/tier-a-golden.json")
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--model", help="Specific model ID")
    args = parser.parse_args()
    
    with open("benchmarks/models.json", 'r') as f: manifest = json.load(f)
    bench = ShirariumBench()
    summaries = []
    models = [m for m in manifest["models"] if not args.model or m["id"] == args.model]
    
    for m in models:
        try:
            res = bench.run_benchmark(m, args.dataset, limit=args.limit)
            if res: summaries.append(res)
        except Exception as e:
            print(f"!! Failed {m['name']}: {e}")
            
    report_path = Path("benchmarks/reports") / f"summary_{int(time.time())}.md"
    summaries.sort(key=lambda x: (-x["acc"], x["lat"]))
    with open(report_path, 'w') as f:
        f.write("# ShirariumBench Summary\n\n| Model | Accuracy | Latency | Params | Quant |\n| :--- | :--- | :--- | :--- | :--- |\n")
        for r in summaries:
            m_meta = next(m for m in manifest["models"] if m["id"] == r["id"])
            f.write(f"| {r['name']} | {r['acc']*100:.1f}% | {r['lat']:.0f}ms | {m_meta['parameters']} | {m_meta['quant']} |\n")
    print(f"\nFinal report: {report_path}")
