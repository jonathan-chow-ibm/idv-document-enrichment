"""Builds per-document CSVs from instances.jsonl captured by extract_doc_detail.py.

Kept separate from extraction so the CSV shape can change without re-querying Durable storage
(the list scan is slow and flaky, and the underlying history is a purge candidate).

Usage: python build_doc_detail_csv.py <label>=<dir> [<label>=<dir> ...] [--lean] [--combined <dir>]
Writes 05-document-detail.csv into each input dir, and a combined CSV with a Batch column.

--lean drops the columns that carry no information on a classify-only run: Routing Decision
(always Review), Write-Back Succeeded (always True), the token columns (extraction is always 0),
Low Confidence Categories (empty without Agent 2 metadata), and Status (always Completed) --
plus the drawing and candidate detail, which is available in report.json when wanted.
"""
import csv
import json
import os
import sys


COLUMNS = [
    "Batch", "Document ID", "File Name", "Folder", "Document Type", "Type Confidence",
    # Agent 1's own wording when its primary pick wasn't a taxonomy label. TolerantDocumentTypeConverter
    # folds those to Other, so without this column the model's actual judgement is invisible.
    "AI Suggested Type",
    "Routing Decision", "Write-Back Succeeded", "Extraction Method", "Pages", "Text Length",
    "Low Confidence Categories", "Candidate Types",
    "Drawing Discipline", "Drawing Sheet", "Drawing Title",
    "Classification Tokens", "Extraction Tokens", "Vision Tokens", "Total Duration ms",
    "Status",
]


LEAN_COLUMNS = [
    "Batch", "File Name", "Document Type", "Type Confidence", "AI Suggested Type",
]


def rows_for(label, directory):
    path = os.path.join(directory, "instances.jsonl")
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            rec = json.loads(line)
            o = rec.get("output") or {}
            ex = o.get("extraction") or {}
            tc = o.get("typeClassification") or {}
            dc = o.get("drawingClassification") or {}
            pm = o.get("processingMetrics") or {}
            fn = o.get("fileName") or ""
            # A failed instance has no output; fall back to the instance id for identity.
            doc_id = o.get("documentId") or (rec.get("instanceId") or "").split(":", 1)[-1]
            cands = "; ".join(
                f"{c.get('documentType')} ({c.get('confidence')})"
                for c in (tc.get("candidates") or []))
            tok = lambda a, b: (pm.get(a) or 0) + (pm.get(b) or 0)  # noqa: E731
            yield {
                "Batch": label,
                "Document ID": doc_id,
                "File Name": fn,
                "Folder": os.path.dirname(fn),
                "Document Type": tc.get("documentType", ""),
                "Type Confidence": tc.get("confidence", ""),
                "AI Suggested Type": tc.get("unrecognizedType") or "",
                "Routing Decision": o.get("routingDecision", ""),
                "Write-Back Succeeded": o.get("writeBackSucceeded", ""),
                "Extraction Method": ex.get("extractionMethod", ""),
                "Pages": ex.get("pageCount", ""),
                "Text Length": ex.get("textLength", ""),
                "Low Confidence Categories": "; ".join(o.get("lowConfidenceCategories") or []),
                "Candidate Types": cands,
                "Drawing Discipline": dc.get("discipline", ""),
                "Drawing Sheet": dc.get("sheetNumber", ""),
                "Drawing Title": dc.get("drawingTitle", ""),
                "Classification Tokens": tok("classificationInputTokens", "classificationOutputTokens"),
                "Extraction Tokens": tok("extractionInputTokens", "extractionOutputTokens"),
                "Vision Tokens": tok("visionInputTokens", "visionOutputTokens"),
                "Total Duration ms": pm.get("totalDurationMs", ""),
                "Status": rec.get("runtimeStatus", ""),
            }


def write(path, rows, with_batch=True, lean=False):
    base = LEAN_COLUMNS if lean else COLUMNS
    cols = base if with_batch else base[1:]
    with open(path, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=cols, extrasaction="ignore")
        w.writeheader()
        for r in sorted(rows, key=lambda r: (r["Batch"], r["File Name"])):
            w.writerow(r)


def main():
    args = sys.argv[1:]
    lean = "--lean" in args
    args = [a for a in args if a != "--lean"]
    combined_dir = None
    if "--combined" in args:
        i = args.index("--combined")
        combined_dir = args[i + 1]
        args = args[:i] + args[i + 2:]

    everything = []
    for spec in args:
        label, directory = spec.split("=", 1)
        rows = list(rows_for(label, directory))
        write(os.path.join(directory, "05-document-detail.csv"), rows, with_batch=False, lean=lean)
        print(f"{directory}/05-document-detail.csv: {len(rows)} rows")
        everything.extend(rows)

    if combined_dir:
        os.makedirs(combined_dir, exist_ok=True)
        write(os.path.join(combined_dir, "05-document-detail.csv"), everything, lean=lean)
        print(f"{combined_dir}/05-document-detail.csv: {len(everything)} rows")


if __name__ == "__main__":
    sys.exit(main())
