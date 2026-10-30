"""
Tripo (tripo3d.ai) from the command line: sculpted models for the game, by text or from a picture,
waited for and downloaded as GLB into Models/<level>/ (from where import_creatures.py and the
level makers take them).

The API key is read from ~/.tripo/api_key (one line, tsk_...) or the TRIPO_API_KEY variable; it is
never printed or written anywhere else.

  python Tools/tripo.py balance
  python Tools/tripo.py text  "<prompt>" Models/lvl2/imp.glb [--negative "..."] [--faces 20000] [--dry]
  python Tools/tripo.py image <picture.png|jpg> Models/lvl2/imp.glb [--faces 20000] [--dry]
  python Tools/tripo.py batch Models/lvl2/batch.json [--dry]
      batch.json: [{"out": "Models/lvl2/imp.glb", "prompt": "..."} | {"out": ..., "image": "ref.png"}, ...]
      (each may also have "negative" and "faces"); a model already downloaded is skipped
  python Tools/tripo.py fetch <task_id> Models/lvl2/imp.glb      (a task started earlier)
  python Tools/tripo.py picture "<prompt>" Models/lvl2/concept.png [--dry]   (a picture: a concept, a reference to model from; 5 credits)

Every generation spends credits: --dry shows what would be sent and spends nothing. The balance
is printed before and after.
"""
import argparse
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
import uuid

API = "https://api.tripo3d.ai/v2/openapi"
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))


def key():
    k = os.environ.get("TRIPO_API_KEY")
    p = os.path.expanduser("~/.tripo/api_key")
    if not k and os.path.exists(p):
        k = open(p, encoding="utf-8").read().strip()
    if not k:
        sys.exit("no Tripo key: put it into ~/.tripo/api_key")
    return k


def call(method, path, body=None, data=None, headers=None, timeout=60):
    h = {"Authorization": "Bearer " + key()}
    if body is not None:
        data = json.dumps(body).encode()
        h["Content-Type"] = "application/json"
    h.update(headers or {})
    req = urllib.request.Request(API + path, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            out = json.loads(r.read())
    except urllib.error.HTTPError as e:
        sys.exit(f"Tripo {method} {path}: HTTP {e.code} {e.read()[:400].decode(errors='replace')}")
    if out.get("code") != 0:
        sys.exit(f"Tripo {method} {path}: {out}")
    return out["data"]


def balance():
    d = call("GET", "/user/balance")
    return d["balance"], d.get("frozen", 0)


def upload(picture):
    """A picture to Tripo; its token for a task."""
    boundary = uuid.uuid4().hex
    name = os.path.basename(picture)
    ctype = mimetypes.guess_type(name)[0] or "image/png"
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{name}\"\r\n"
            f"Content-Type: {ctype}\r\n\r\n").encode() + open(picture, "rb").read() + f"\r\n--{boundary}--\r\n".encode()
    d = call("POST", "/upload", data=body, headers={"Content-Type": f"multipart/form-data; boundary={boundary}"}, timeout=120)
    return d["image_token"], ("jpg" if ctype.endswith("jpeg") else ctype.split("/")[-1])


def task_body(job):
    if job.get("picture"):
        return {"type": "generate_image", "prompt": job["prompt"]}
    b = {"texture": True, "pbr": True}
    if job.get("faces"):
        b["face_limit"] = int(job["faces"])
    if "image" in job:
        b["type"] = "image_to_model"
        b["file"] = {"type": "<uploaded>", "file_token": "<uploaded>"}
    else:
        b["type"] = "text_to_model"
        b["prompt"] = job["prompt"]
        if job.get("negative"):
            b["negative_prompt"] = job["negative"]
    return b


def start(job):
    b = task_body(job)
    if "image" in job:
        token, kind = upload(job["image"])
        b["file"] = {"type": kind, "file_token": token}
    return call("POST", "/task", b)["task_id"]


def wait(task_id, label=""):
    last = None
    while True:
        d = call("GET", f"/task/{task_id}")
        st, pr = d.get("status"), d.get("progress", 0)
        if (st, pr) != last:
            print(f"  {label} {st} {pr}%", flush=True)
            last = (st, pr)
        if st == "success":
            return d
        if st in ("failed", "cancelled", "unknown", "banned", "expired"):
            sys.exit(f"Tripo task {task_id} ({label}): {st}")
        time.sleep(4)


def download(d, out):
    o = d.get("output") or {}
    url = o.get("pbr_model") or o.get("model") or o.get("base_model") or o.get("generated_image")
    if not url:
        sys.exit(f"Tripo task {d.get('task_id')}: no model in its output ({list(o)})")
    path = os.path.join(ROOT, out) if not os.path.isabs(out) else out
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with urllib.request.urlopen(url, timeout=300) as r, open(path, "wb") as f:
        f.write(r.read())
    print(f"  saved {out} ({os.path.getsize(path) // 1024} KB)")


def run(jobs, dry):
    todo = [j for j in jobs if not os.path.exists(os.path.join(ROOT, j["out"]))]
    for j in jobs:
        if j not in todo:
            print("already there:", j["out"])
    if not todo:
        return
    print(f"{len(todo)} model(s) to make")
    for j in todo:
        print(" ", j["out"], "<-", ("picture " + j["image"]) if "image" in j else repr(j["prompt"][:90]))
    if dry:
        for j in todo:
            print("  would send", json.dumps(task_body(j), ensure_ascii=False))
        return
    b0, _ = balance()
    print("balance before:", b0)
    for j in todo:
        tid = start(j)
        print(" task", tid, "for", j["out"])
        download(wait(tid, os.path.basename(j["out"])), j["out"])
    b1, _ = balance()
    print(f"balance after: {b1} (spent {b0 - b1})")


def main():
    ap = argparse.ArgumentParser(description="Tripo models for the game")
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("balance")
    for name in ("text", "image"):
        p = sub.add_parser(name)
        p.add_argument("what")
        p.add_argument("out")
        p.add_argument("--negative")
        p.add_argument("--faces", type=int)
        p.add_argument("--dry", action="store_true")
    p = sub.add_parser("batch")
    p.add_argument("file")
    p.add_argument("--dry", action="store_true")
    p = sub.add_parser("picture")
    p.add_argument("what")
    p.add_argument("out")
    p.add_argument("--dry", action="store_true")
    p = sub.add_parser("fetch")
    p.add_argument("task_id")
    p.add_argument("out")
    a = ap.parse_args()
    if a.cmd == "balance":
        b, f = balance()
        print(f"balance {b}, frozen {f}")
    elif a.cmd in ("text", "image"):
        job = {"out": a.out, "faces": a.faces}
        if a.cmd == "text":
            job["prompt"] = a.what
            job["negative"] = a.negative
        else:
            job["image"] = a.what
        run([job], a.dry)
    elif a.cmd == "picture":
        run([{"out": a.out, "prompt": a.what, "picture": True}], a.dry)
    elif a.cmd == "batch":
        base = os.path.dirname(os.path.abspath(a.file))
        jobs = json.load(open(a.file, encoding="utf-8"))
        for j in jobs:
            if "image" in j and not os.path.isabs(j["image"]):
                j["image"] = os.path.join(base, j["image"])
        run(jobs, a.dry)
    elif a.cmd == "fetch":
        download(wait(a.task_id, os.path.basename(a.out)), a.out)


if __name__ == "__main__":
    main()
