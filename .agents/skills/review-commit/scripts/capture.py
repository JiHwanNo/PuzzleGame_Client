"""검증을 시작한 시각과 해당 작업 트리의 커밋을 실행별로 기록한다."""
import json
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import tempfile


def git(*args):
    return subprocess.check_output(["git", *args], text=True, encoding="utf-8").strip()


def main():
    captured = datetime.now(timezone.utc)
    commit = git("rev-parse", "--verify", "HEAD^{commit}")
    state = Path(git("rev-parse", "--absolute-git-dir")) / "review-commit"
    state.mkdir(exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix=captured.strftime("%Y%m%dT%H%M%SZ-"), dir=state))
    record = {"captured_at": captured.isoformat(), "commit": commit}
    (run / "snapshot.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(run)


if __name__ == "__main__":
    main()
