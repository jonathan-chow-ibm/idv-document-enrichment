"""Extracts per-document detail for a batch from the Durable instances store.

Why not App Insights: `DocumentEnriched` customEvents are sampled (host.json excludes only
Request;Dependency), and for batch b81a6279 (Anserra) they were lost outright -- zero rows
survive for that batchId, while Beechnut's are complete. The Durable store is the only source
that covers both, and it carries MORE per document than the telemetry ever did: metadata,
per-document token metrics, drawing classification and write-back status.

The instance-list query is a storage table scan and fails with DurableTaskStorageException at
large `top`. It works at top=50, paginated via the x-ms-continuation-token header, so that is
what this does -- with retries, because the same scan intermittently fails even when small.

Writes instances.jsonl (raw EnrichmentResult per document) before deriving any CSV, so the
extraction survives a Durable history purge and CSVs can be rebuilt without re-querying.

Usage: DTKEY=... python extract_doc_detail.py <batchId> <out_dir>
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request

KEY = os.environ["DTKEY"]
BASE = "https://func-idv-doc-enrich-dev.azurewebsites.net/runtime/webhooks/durabletask/instances"
PAGE = 50
MAX_RETRIES = 6


def fetch_page(batch, token):
    url = f"{BASE}?code={KEY}&instanceIdPrefix={batch}&top={PAGE}&showOutput=true"
    req = urllib.request.Request(url)
    if token:
        req.add_header("x-ms-continuation-token", token)
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                body = r.read().decode()
                next_token = r.headers.get("x-ms-continuation-token")
            rows = json.loads(body)
            if not isinstance(rows, list):
                raise RuntimeError("storage error response")
            return rows, next_token
        except Exception as exc:  # noqa: BLE001 - the scan flakes; back off and retry
            if attempt == MAX_RETRIES:
                raise
            wait = 2 ** attempt
            print(f"    page retry {attempt}/{MAX_RETRIES} after {type(exc).__name__}; waiting {wait}s",
                  flush=True)
            time.sleep(wait)
    return [], None


def main():
    batch, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, "instances.jsonl")

    token, pages, docs, others = None, 0, 0, 0
    with open(path, "w", encoding="utf-8") as fh:
        while True:
            rows, token = fetch_page(batch, token)
            pages += 1
            for row in rows:
                name = row.get("name")
                if name != "DocumentProcessingOrchestrator":
                    others += 1
                    continue
                out = row.get("output")
                if isinstance(out, str):
                    try:
                        out = json.loads(out)
                    except ValueError:
                        out = None
                fh.write(json.dumps({
                    "instanceId": row.get("instanceId"),
                    "runtimeStatus": row.get("runtimeStatus"),
                    "createdTime": row.get("createdTime"),
                    "lastUpdatedTime": row.get("lastUpdatedTime"),
                    "output": out,
                }, separators=(",", ":")) + "\n")
                docs += 1
            if pages % 10 == 0 or not token:
                print(f"  page {pages}: {docs} document instances so far", flush=True)
            if not token or not rows:
                break

    print(f"done: {docs} document instances, {others} non-document rows, {pages} pages -> {path}")


if __name__ == "__main__":
    sys.exit(main())
