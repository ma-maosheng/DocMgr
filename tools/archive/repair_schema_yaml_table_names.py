# -*- coding: utf-8 -*-
"""Repair corrupted field chineseName indentation and set table-level Chinese names."""
from __future__ import annotations

import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
YAML_PATH = REPO / ".cursor" / "schema" / "SchemaDictionary.yaml"
REVIEW_MAP_PATH = REPO / "Infrastructure" / "Seeding" / "FieldDomainSeedService.ReviewFieldAliasMaps.cs"
ALIAS_MAP_PATH = REPO / "Infrastructure" / "Seeding" / "FieldDomainSeedService.AliasMaps.cs"

TABLE_CHINESE_NAMES = {
    "BusinessLogicSettings": "业务逻辑设置",
    "YearlyArchiveFilingFact": "立档事实",
    "YearlyArchiveMaterialTransaction": "资料流转履历",
    "YearlyArchiveOutboundItem": "资料出库明细",
    "YearlyArchiveOutboundRecord": "资料出库单",
    "YearlyArchiveOutboundSyncEntry": "资料出库同步条目",
    "YearlyArchiveRelocationItem": "资料移库明细",
    "YearlyArchiveRelocationRecord": "资料移库单",
    "YearlyArchiveReturnItem": "资料归还明细",
    "YearlyArchiveReturnRecord": "资料归还单",
    "YearlyArchiveSearchResultSet": "资料检索结果集",
    "YearlyArchiveSearchResultSetItem": "资料检索结果集明细",
    "YearlyElectronicArchiveUnitMediaItemLink": "电子立档单元-介质明细关联",
}


def load_cs_alias_map(path: Path, dict_name: str) -> dict[str, str]:
    text = path.read_text(encoding="utf-8-sig")
    start = text.find(dict_name)
    if start < 0:
        return {}
    chunk = text[start:]
    result: dict[str, str] = {}
    for match in re.finditer(r'\["([^"]+)"\]\s*=\s*"([^"]*)"', chunk):
        result[match.group(1)] = match.group(2)
    return result


def resolve_field_alias(entity: str, field: str, aliases: dict[str, str]) -> str:
    exact_key = f"{entity}.{field}"
    if exact_key in aliases:
        return aliases[exact_key]
    if field in aliases:
        return aliases[field]
    if field == "Id":
        return "ID"
    return field


def repair_yaml() -> None:
    aliases = load_cs_alias_map(REVIEW_MAP_PATH, "ReviewFieldAliasMap")
    aliases.update(load_cs_alias_map(ALIAS_MAP_PATH, "ExactAliasMap"))
    aliases.update(load_cs_alias_map(ALIAS_MAP_PATH, "FieldAliasMap"))

    lines = YAML_PATH.read_text(encoding="utf-8").splitlines()
    entity: str | None = None
    field: str | None = None
    in_fields = False
    table_chinese_set = False
    out: list[str] = []
    fixed_fields = 0

    for line in lines:
        entity_match = re.match(r"^  ([A-Za-z][A-Za-z0-9]*):\s*$", line)
        if entity_match:
            entity = entity_match.group(1)
            in_fields = False
            table_chinese_set = False
            out.append(line)
            continue

        if line.strip() == "fields:":
            in_fields = True
            out.append(line)
            continue

        if in_fields:
            field_match = re.match(r"^      ([A-Za-z][A-Za-z0-9]*):\s*$", line)
            if field_match:
                field = field_match.group(1)
                out.append(line)
                continue

            if field and re.match(r"^    chineseName:", line):
                chinese_name = resolve_field_alias(entity or "", field, aliases)
                out.append(f"        chineseName: {chinese_name}")
                fixed_fields += 1
                continue

            if re.match(r"^        chineseName:", line):
                field = None
                out.append(line)
                continue

            out.append(line)
            continue

        if entity and not table_chinese_set and line.strip().startswith("chineseName:"):
            chinese_name = TABLE_CHINESE_NAMES.get(entity, line.split(":", 1)[1].strip())
            out.append(f"    chineseName: {chinese_name}")
            table_chinese_set = True
            continue

        out.append(line)

    YAML_PATH.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"Repaired field chineseName lines: {fixed_fields}")
    print(f"Table Chinese names set: {len(TABLE_CHINESE_NAMES)}")


if __name__ == "__main__":
    repair_yaml()
