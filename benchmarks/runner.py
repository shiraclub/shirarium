import json
import time
import argparse
import requests
import os
import subprocess
import signal
import zipfile
from pathlib import Path
from typing import Dict, Any, List

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

    def ensure_llama_server(self) -> str:
        binary_name = "llama-server.exe" if os.name == 'nt' else "llama-server"
        local_binary = self.bin_dir / binary_name
        if local_binary.exists(): return str(local_binary)
        try:
            subprocess.check_call([binary_name, "--version"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            return binary_name
        except: pass

        print(f"llama-server not found. Downloading for {os.name}...")
        base_url = "https://github.com/ggml-org/llama.cpp/releases/download/b4640/"
        if os.name == 'nt':
            url = base_url + "llama-b4640-bin-win-vulkan-x64.zip"
            zip_path = self.bin_dir / "llama.zip"
            subprocess.check_call(["curl", "-L", url, "-o", str(zip_path)])
            with zipfile.ZipFile(zip_path, 'r') as zip_ref:
                zip_ref.extractall(self.bin_dir)
            os.remove(zip_path)
            for p in self.bin_dir.rglob("llama-server.exe"): return str(p)
        return binary_name

    def download_model(self, model_info: Dict[str, Any]):
        target = self.models_dir / model_info["filename"]
        if target.exists(): return target
        print(f"Downloading {model_info['name']}...")
        subprocess.check_call(["curl", "-L", model_info["url"], "-o", str(target)])
        return target

    def start_server(self, model_path: Path, binary_path: str):
        cmd = [binary_path, "-m", str(model_path), "--port", str(self.port), "--n-gpu-layers", "0", "--ctx-size", "2048"]
        process = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                 creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0)
        for _ in range(60):
            try:
                if requests.get(f"{self.api_url}/health", timeout=1).status_code == 200: return process
            except: pass
            time.sleep(2)
        process.kill()
        raise Exception("Server failed to start within 120 seconds.")

    def parse_with_llm(self, filename: str) -> Dict[str, Any]:
        # ADVANCED SYSTEM PROMPT
        system_prompt = (
            "You are a deterministic data extraction engine. Parse the provided media filename into a strict JSON object.\n"
            "Rules:\n"
            "- release_year: choose the release year (often in parentheses or near resolution).\n"
            "- title: the name of the media, including title years if applicable (e.g. '2012' the movie).\n"
            "- Use null for missing values. No conversational text."
        )

        # ADVERSARIAL FEW-SHOT EXAMPLES
        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": "1984 (1984) 1080p BluRay.mkv"},
            {"role": "assistant", "content": '{"Title": "1984", "Year": 1984, "Resolution": "1080p"}'},
            {"role": "user", "content": "2012.2009.1080p.mkv"},
            {"role": "assistant", "content": '{"Title": "2012", "Year": 2009, "Resolution": "1080p"}'},
            {"role": "user", "content": filename}
        ]

        # STRICT JSON SCHEMA ENFORCEMENT
        schema = {
            "type": "object",
            "properties": {
                "Title": {"type": "string"},
                "Year": {"type": ["number", "null"]},
                "Season": {"type": ["number", "null"]},
                "Episode": {"type": ["number", "null"]},
                "Resolution": {"type": ["string", "null"]}
            },
            "required": ["Title", "Year", "Season", "Episode", "Resolution"]
        }

        start_time = time.perf_counter()
        try:
            response = requests.post(
                f"{self.api_url}/v1/chat/completions",
                json={
                    "messages": messages,
                    "temperature": 0.0,
                    "response_format": {"type": "json_object", "schema": schema}
                },
                timeout=60
            )
            result_text = response.json()["choices"][0]["message"]["content"]
        except Exception as e:
            result_text = json.dumps({"error": str(e)})

        latency = (time.perf_counter() - start_time) * 1000
        try:
            parsed = json.loads(result_text)
        except:
            parsed = {"error": "Invalid JSON", "raw": result_text}
        return parsed, latency

    def calculate_score(self, expected: Dict[str, Any], actual: Dict[str, Any]) -> Dict[str, float]:
        fields = ["Title", "Year", "Season", "Episode", "Resolution"]
        scores = {}
        total_correct = 0
        for field in fields:
            exp_val = expected.get(field) or expected.get(field.lower())
            act_val = actual.get(field) or actual.get(field.lower())
            if str(exp_val).lower().strip() == str(act_val).lower().strip():
                scores[field] = 1.0
                total_correct += 1
            else:
                scores[field] = 0.0
        scores["Overall"] = total_correct / len(fields)
        return scores

    def run_benchmark(self, model_info: Dict[str, Any], dataset_path: str, binary_path: str = None, limit: int = 0):
        print(f"\n>>> Model: {model_info['name']} ({model_info['quant']})")
        if not binary_path: binary_path = self.ensure_llama_server()
        model_path = self.download_model(model_info)
        server_process = self.start_server(model_path, binary_path)
        try:
            with open(dataset_path, 'r', encoding='utf-8') as f:
                items = json.load(f).get("entries", [])
            if limit > 0: items = items[:limit]
            results = []
            total_latency, total_accuracy = 0, 0
            for idx, item in enumerate(items):
                filename = os.path.basename(item.get("relativePath", ""))
                expected = item.get("expected", item)
                print(f"[{idx+1}/{len(items)}] {filename[:40]}...", end="\r")
                actual, latency = self.parse_with_llm(filename)
                scores = self.calculate_score(expected, actual)
                results.append({"filename": filename, "scores": scores, "latency": latency})
                total_latency += latency
                total_accuracy += scores["Overall"]
            avg_latency, avg_accuracy = total_latency / len(items), total_accuracy / len(items)
            print(f"\nResult: Acc={avg_accuracy*100:.1f}%, Latency={avg_latency:.0f}ms")
            return {"id": model_info["id"], "name": model_info["name"], "acc": avg_accuracy, "lat": avg_latency}
        finally:
            subprocess.call(['taskkill', '/F', '/T', '/PID', str(server_process.pid)])

if __name__ == "__main__":
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
        try: summaries.append(bench.run_benchmark(m, args.dataset, limit=args.limit))
        except Exception as e: print(f"Failed {m['name']}: {e}")
    report_path = Path("benchmarks/reports") / f"summary_{int(time.time())}.md"
    summaries.sort(key=lambda x: (-x["acc"], x["lat"]))
    with open(report_path, 'w') as f:
        f.write("# ShirariumBench SOTA Comparison\n\n| Model | Accuracy | Latency | Params | Quant |\n| :--- | :--- | :--- | :--- | :--- |\n")
        for r in summaries:
            m_meta = next(m for m in manifest["models"] if m["id"] == r["id"])
            f.write(f"| {r['name']} | {r['acc']*100:.1f}% | {r['lat']:.0f}ms | {m_meta['parameters']} | {m_meta['quant']} |\n")
    print(f"\nFinal summary: {report_path}")
