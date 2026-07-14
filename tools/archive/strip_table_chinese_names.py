"""One-time helper: remove ChineseName from AdvancedDataTableMetadata entries."""
import re
from pathlib import Path

p = Path(__file__).resolve().parents[1] / "Services/SystemSettings/AdvancedDataTableMetadata.cs"
text = p.read_text(encoding="utf-8")
text = text.replace(
    "internal sealed record TableMetadataEntry(\n        string ChineseName,\n        string Description,",
    "internal sealed record TableMetadataEntry(\n        string Description,",
)

lines = text.splitlines()
out = []
skip_next_string = False
for line in lines:
    if re.search(r"\]\s*=\s*new\(\s*$", line):
        skip_next_string = True
        out.append(line)
        continue
    if skip_next_string and re.match(r'\s+".*",\s*$', line):
        skip_next_string = False
        continue
    out.append(line)

p.write_text("\n".join(out) + "\n", encoding="utf-8")
print("Updated", p)
