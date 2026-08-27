namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档盒号拆分、解析与混放关联组（连通分量）。
    /// </summary>
    public static class HistoryArchiveBoxCodeSupport
    {
        /// <summary>按分隔符拆分盒号字段，去空白、去重。</summary>
        public static IReadOnlyList<string> SplitBoxCodes(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return Array.Empty<string>();
            }

            return source
                .Split([';', '；', ',', '，', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>盒号字段是否列出多个盒（开柜混放待梳理）。</summary>
        public static bool IsMixedPlacementBoxNumber(string? source) => SplitBoxCodes(source).Count > 1;

        /// <summary>盒号字段是否包含指定盒号。</summary>
        public static bool ContainsBoxCode(string? source, string? boxCode)
        {
            string target = boxCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            return SplitBoxCodes(source).Any(code =>
                string.Equals(code, target, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 解析四段盒号（柜名+面-层-列-序号）。无法解析返回 false。
        /// </summary>
        public static bool TryParseBoxCode(
            string? boxCode,
            out string cabinetName,
            out string faceCode,
            out string slotCode,
            out string normalizedBoxCode)
        {
            cabinetName = string.Empty;
            faceCode = string.Empty;
            slotCode = string.Empty;
            normalizedBoxCode = string.Empty;

            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return false;
            }

            string trimmed = boxCode.Trim();
            var segments = trimmed.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 4)
            {
                return false;
            }

            string cabinetAndFace = segments[0];
            if (cabinetAndFace.Length < 2)
            {
                return false;
            }

            char faceChar = cabinetAndFace[^1];
            string face = faceChar switch
            {
                'A' or 'a' => "A",
                'B' or 'b' => "B",
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(face)
                || !int.TryParse(segments[1], out int layerIndex)
                || !int.TryParse(segments[2], out _))
            {
                return false;
            }

            cabinetName = cabinetAndFace[..^1].Trim();
            faceCode = face;
            slotCode = $"{layerIndex}-{segments[2].Trim()}";
            normalizedBoxCode = trimmed;
            return !string.IsNullOrWhiteSpace(cabinetName);
        }

        /// <summary>
        /// 按台账盒号共现构建关联组：同一字段内的盒号互相关联，再取传递闭包。
        /// 返回每个盒号对应的整组（含自身），按盒号忽略大小写。
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildRelatedGroups(
            IEnumerable<string?> boxNumberFields)
        {
            var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in boxNumberFields)
            {
                IReadOnlyList<string> codes = SplitBoxCodes(field);
                if (codes.Count == 0)
                {
                    continue;
                }

                string first = codes[0];
                EnsureNode(parent, first);
                for (int index = 1; index < codes.Count; index++)
                {
                    EnsureNode(parent, codes[index]);
                    Union(parent, first, codes[index]);
                }
            }

            var members = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string code in parent.Keys)
            {
                string root = Find(parent, code);
                if (!members.TryGetValue(root, out List<string>? list))
                {
                    list = new List<string>();
                    members[root] = list;
                }

                list.Add(code);
            }

            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in members.Values)
            {
                IReadOnlyList<string> ordered = group
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (string code in ordered)
                {
                    result[code] = ordered;
                }
            }

            return result;
        }

        /// <summary>从关联组字典取指定盒号的整组；无记录时仅返回自身。</summary>
        public static IReadOnlyList<string> ResolveRelatedGroup(
            IReadOnlyDictionary<string, IReadOnlyList<string>> groups,
            string? boxCode)
        {
            string code = boxCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return Array.Empty<string>();
            }

            if (groups.TryGetValue(code, out IReadOnlyList<string>? related) && related.Count > 0)
            {
                return related;
            }

            return new[] { code };
        }

        /// <summary>从盒号字段去掉指定盒号后重新拼接（中文分号）。</summary>
        public static string RemoveBoxCodes(string? source, IEnumerable<string> boxCodesToRemove)
        {
            var remove = new HashSet<string>(
                (boxCodesToRemove ?? Array.Empty<string>())
                    .Select(item => item?.Trim() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item)),
                StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> remaining = SplitBoxCodes(source)
                .Where(code => !remove.Contains(code))
                .ToList();
            return remaining.Count == 0 ? string.Empty : string.Join("；", remaining);
        }

        private static void EnsureNode(Dictionary<string, string> parent, string code)
        {
            if (!parent.ContainsKey(code))
            {
                parent[code] = code;
            }
        }

        private static string Find(Dictionary<string, string> parent, string code)
        {
            string current = code;
            while (!string.Equals(parent[current], current, StringComparison.OrdinalIgnoreCase))
            {
                parent[current] = parent[parent[current]];
                current = parent[current];
            }

            return current;
        }

        private static void Union(Dictionary<string, string> parent, string left, string right)
        {
            string rootLeft = Find(parent, left);
            string rootRight = Find(parent, right);
            if (!string.Equals(rootLeft, rootRight, StringComparison.OrdinalIgnoreCase))
            {
                parent[rootRight] = rootLeft;
            }
        }
    }
}
