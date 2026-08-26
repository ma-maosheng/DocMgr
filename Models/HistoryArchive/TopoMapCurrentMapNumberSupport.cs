using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 按 GB/T 13989《国家基本比例尺地形图分幅和编号》将历史图上图号换算为现行图号。
    /// </summary>
    public static class TopoMapCurrentMapNumberSupport
    {
        private static readonly Regex NewFormatMillionRegex = new(
            @"^[A-V]\d{2}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex NewFormatScaledRegex = new(
            @"^[A-V]\d{2}[B-K]\d{6}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Dictionary<int, char> ScaleCharByDenominator = new()
        {
            [500_000] = 'B',
            [250_000] = 'C',
            [100_000] = 'D',
            [50_000] = 'E',
            [25_000] = 'F',
            [10_000] = 'G',
            [5_000] = 'H',
            [2_000] = 'I',
            [1_000] = 'J',
            [500] = 'K',
        };

        private static readonly Dictionary<int, (int Rows, int Cols)> GridByDenominator = new()
        {
            [500_000] = (2, 2),
            [250_000] = (4, 4),
            [100_000] = (12, 12),
            [50_000] = (24, 24),
            [25_000] = (48, 48),
            [10_000] = (96, 96),
            [5_000] = (192, 192),
            [2_000] = (576, 576),
            [1_000] = (1152, 1152),
            [500] = (2304, 2304),
        };

        /// <summary>
        /// 规范化图号：去除空白，并将全角括号/破折号等替换为半角。
        /// </summary>
        public static string NormalizeMapNumber(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                builder.Append(ch switch
                {
                    '（' => '(',
                    '）' => ')',
                    '【' => '[',
                    '】' => ']',
                    '－' or '—' or '–' => '-',
                    '：' or '∶' => ':',
                    '，' => ',',
                    '、' => '、',
                    _ => ch
                });
            }

            return builder.ToString();
        }

        /// <summary>
        /// 根据比例尺与图上图号计算现行图号；无法识别时返回空字符串。
        /// </summary>
        public static string Compute(string? scale, string? mapNumber)
        {
            return TryCompute(scale, mapNumber, out string current) ? current : string.Empty;
        }

        /// <summary>
        /// 尝试根据比例尺与图上图号计算现行图号。
        /// </summary>
        public static bool TryCompute(string? scale, string? mapNumber, out string currentMapNumber)
        {
            currentMapNumber = string.Empty;
            string normalized = NormalizeMapNumber(mapNumber);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            var parts = SplitMapNumberParts(normalized);
            if (parts.Count == 0)
            {
                return false;
            }

            if (!TryParseScaleDenominator(scale, out int denominator))
            {
                // 已是现行编号时，即使比例尺无法解析也直接归一化返回。
                if (parts.Count == 1 && TryNormalizeExistingCurrentNumber(parts[0], out currentMapNumber))
                {
                    return true;
                }

                return false;
            }

            var converted = new List<string>(parts.Count);
            foreach (string part in parts)
            {
                if (TryConvertOne(denominator, part, out string one))
                {
                    converted.Add(one);
                    continue;
                }

                return false;
            }

            currentMapNumber = string.Join("、", converted);
            return !string.IsNullOrWhiteSpace(currentMapNumber);
        }

        /// <summary>
        /// 为缺失「当前图号」的记录就地补全；有变更时返回 <see langword="true"/>。
        /// </summary>
        public static bool FillMissing(IEnumerable<TopoMap> maps)
        {
            ArgumentNullException.ThrowIfNull(maps);

            bool changed = false;
            foreach (TopoMap map in maps)
            {
                if (!string.IsNullOrWhiteSpace(map.CurrentMapNumber))
                {
                    continue;
                }

                string computed = Compute(map.Scale, map.MapNumber);
                if (string.IsNullOrWhiteSpace(computed))
                {
                    continue;
                }

                map.CurrentMapNumber = computed;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// 按现行规则重算并写回 <see cref="TopoMap.CurrentMapNumber"/>。
        /// </summary>
        public static void Apply(TopoMap map)
        {
            ArgumentNullException.ThrowIfNull(map);
            map.CurrentMapNumber = Compute(map.Scale, map.MapNumber);
        }

        private static IReadOnlyList<string> SplitMapNumberParts(string normalized)
        {
            string[] rawParts = normalized.Split(
                ['、', ',', ';', '；'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return rawParts.Length == 0
                ? Array.Empty<string>()
                : rawParts;
        }

        private static bool TryConvertOne(int denominator, string mapNumber, out string currentMapNumber)
        {
            currentMapNumber = string.Empty;
            if (TryNormalizeExistingCurrentNumber(mapNumber, out currentMapNumber))
            {
                return true;
            }

            if (!TryParseMillionthAndTokens(mapNumber, out char rowLetter, out int millionCol, out IReadOnlyList<string> tokens))
            {
                return false;
            }

            if (denominator == 1_000_000)
            {
                currentMapNumber = FormatMillionth(rowLetter, millionCol);
                return true;
            }

            if (!ScaleCharByDenominator.TryGetValue(denominator, out char scaleChar)
                || !GridByDenominator.TryGetValue(denominator, out var grid))
            {
                return false;
            }

            if (!TryResolveGridPosition(denominator, tokens, out int gridRow, out int gridCol))
            {
                return false;
            }

            if (gridRow < 1 || gridRow > grid.Rows || gridCol < 1 || gridCol > grid.Cols)
            {
                return false;
            }

            currentMapNumber = $"{FormatMillionth(rowLetter, millionCol)}{scaleChar}{gridRow:D3}{gridCol:D3}";
            return true;
        }

        private static bool TryNormalizeExistingCurrentNumber(string mapNumber, out string currentMapNumber)
        {
            currentMapNumber = string.Empty;
            string compact = mapNumber.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            if (NewFormatMillionRegex.IsMatch(compact) || NewFormatScaledRegex.IsMatch(compact))
            {
                currentMapNumber = compact;
                return true;
            }

            return false;
        }

        private static bool TryParseMillionthAndTokens(
            string mapNumber,
            out char rowLetter,
            out int millionCol,
            out IReadOnlyList<string> tokens)
        {
            rowLetter = '\0';
            millionCol = 0;
            tokens = Array.Empty<string>();

            string text = mapNumber.Trim();
            if (text.Length == 0)
            {
                return false;
            }

            // 紧凑形式：H4812A / H48[9] / H4812(24)
            Match compact = Regex.Match(
                text,
                @"^([A-Va-v]|[1-9]|1[0-9]|2[0-2])(\d{2})(.*)$",
                RegexOptions.CultureInvariant);
            if (compact.Success && !text.Contains('-', StringComparison.Ordinal))
            {
                if (!TryParseRowLetter(compact.Groups[1].Value, out rowLetter))
                {
                    return false;
                }

                if (!int.TryParse(compact.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out millionCol)
                    || millionCol is < 1 or > 60)
                {
                    return false;
                }

                tokens = ExtractTokens(compact.Groups[3].Value);
                return true;
            }

            // 标准旧式：H-48-12-A / 9-48-12-(24) / H-48-[9]
            Match hyphenated = Regex.Match(
                text,
                @"^([A-Va-v]|[1-9]|1[0-9]|2[0-2])[-](\d{1,2})(?:[-](.*))?$",
                RegexOptions.CultureInvariant);
            if (!hyphenated.Success)
            {
                return false;
            }

            if (!TryParseRowLetter(hyphenated.Groups[1].Value, out rowLetter))
            {
                return false;
            }

            if (!int.TryParse(hyphenated.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out millionCol)
                || millionCol is < 1 or > 60)
            {
                return false;
            }

            tokens = hyphenated.Groups[3].Success
                ? ExtractTokens(hyphenated.Groups[3].Value)
                : Array.Empty<string>();
            return true;
        }

        private static IReadOnlyList<string> ExtractTokens(string remaining)
        {
            if (string.IsNullOrWhiteSpace(remaining))
            {
                return Array.Empty<string>();
            }

            var tokens = new List<string>();
            foreach (Match match in Regex.Matches(
                         remaining,
                         @"\((\d+)\)|\[(\d+)\]|【(\d+)】|([A-Da-d甲乙丙丁一二三四])|(\d+)",
                         RegexOptions.CultureInvariant))
            {
                string token = match.Groups[1].Success ? match.Groups[1].Value
                    : match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Success ? match.Groups[3].Value
                    : match.Groups[4].Success ? match.Groups[4].Value
                    : match.Groups[5].Value;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        private static bool TryResolveGridPosition(
            int denominator,
            IReadOnlyList<string> tokens,
            out int gridRow,
            out int gridCol)
        {
            gridRow = 0;
            gridCol = 0;

            return denominator switch
            {
                500_000 => TryResolve500k(tokens, out gridRow, out gridCol),
                250_000 => TryResolve250k(tokens, out gridRow, out gridCol),
                100_000 => TryResolve100k(tokens, out gridRow, out gridCol),
                50_000 => TryResolve50k(tokens, out gridRow, out gridCol),
                25_000 => TryResolve25k(tokens, out gridRow, out gridCol),
                10_000 => TryResolve10k(tokens, out gridRow, out gridCol),
                5_000 => TryResolve5k(tokens, out gridRow, out gridCol),
                2_000 => TryResolve2k(tokens, out gridRow, out gridCol),
                1_000 => TryResolve1k(tokens, out gridRow, out gridCol),
                500 => TryResolve500(tokens, out gridRow, out gridCol),
                _ => false
            };
        }

        private static bool TryResolve500k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseQuadrant(tokens[0], out int rowOff, out int colOff))
            {
                return false;
            }

            row = rowOff + 1;
            col = colOff + 1;
            return true;
        }

        private static bool TryResolve250k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseSerial(tokens[0], 1, 16, out int serial))
            {
                return false;
            }

            return TryRowColFromSerial(serial, columns: 4, out row, out col);
        }

        private static bool TryResolve100k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseSerial(tokens[0], 1, 144, out int serial))
            {
                return false;
            }

            return TryRowColFromSerial(serial, columns: 12, out row, out col);
        }

        private static bool TryResolve50k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 2
                || !TryParseSerial(tokens[0], 1, 144, out int sheet100k)
                || !TryParseQuadrant(tokens[1], out int rowOff, out int colOff)
                || !TryRowColFromSerial(sheet100k, columns: 12, out int r100, out int c100))
            {
                return false;
            }

            row = (r100 - 1) * 2 + rowOff + 1;
            col = (c100 - 1) * 2 + colOff + 1;
            return true;
        }

        private static bool TryResolve25k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;

            // H-48-12-A-1
            if (tokens.Count >= 3
                && TryParseSerial(tokens[0], 1, 144, out int sheet100k)
                && TryParseQuadrant(tokens[1], out int q50Row, out int q50Col)
                && TryParseQuadrant(tokens[2], out int q25Row, out int q25Col)
                && TryRowColFromSerial(sheet100k, columns: 12, out int r100, out int c100))
            {
                int r50 = (r100 - 1) * 2 + q50Row + 1;
                int c50 = (c100 - 1) * 2 + q50Col + 1;
                row = (r50 - 1) * 2 + q25Row + 1;
                col = (c50 - 1) * 2 + q25Col + 1;
                return true;
            }

            // H-48-12-(5)：1:100k 内 4×4 编 1–16
            if (tokens.Count >= 2
                && TryParseSerial(tokens[0], 1, 144, out sheet100k)
                && TryParseSerial(tokens[1], 1, 16, out int serial25)
                && TryRowColFromSerial(sheet100k, columns: 12, out r100, out c100)
                && TryRowColFromSerial(serial25, columns: 4, out int r4, out int c4))
            {
                row = (r100 - 1) * 4 + r4;
                col = (c100 - 1) * 4 + c4;
                return true;
            }

            return false;
        }

        private static bool TryResolve10k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;

            // H-48-12-(24)：1:100k 内 8×8 编 1–64
            if (tokens.Count >= 2
                && TryParseSerial(tokens[0], 1, 144, out int sheet100k)
                && TryParseSerial(tokens[1], 1, 64, out int serial64)
                && tokens.Count == 2
                && TryRowColFromSerial(sheet100k, columns: 12, out int r100, out int c100)
                && TryRowColFromSerial(serial64, columns: 8, out int r8, out int c8))
            {
                row = (r100 - 1) * 8 + r8;
                col = (c100 - 1) * 8 + c8;
                return true;
            }

            // H-48-12-A-(8)：1:50k 内 4×4 编 1–16
            if (tokens.Count >= 3
                && TryParseSerial(tokens[0], 1, 144, out sheet100k)
                && TryParseQuadrant(tokens[1], out int q50Row, out int q50Col)
                && TryParseSerial(tokens[2], 1, 16, out int serial16)
                && tokens.Count == 3
                && TryRowColFromSerial(sheet100k, columns: 12, out r100, out c100)
                && TryRowColFromSerial(serial16, columns: 4, out int r4, out int c4))
            {
                int r50 = (r100 - 1) * 2 + q50Row + 1;
                int c50 = (c100 - 1) * 2 + q50Col + 1;
                row = (r50 - 1) * 4 + r4;
                col = (c50 - 1) * 4 + c4;
                return true;
            }

            // H-48-12-A-1-(3)：四级 2×2 细分
            if (tokens.Count >= 4
                && TryParseSerial(tokens[0], 1, 144, out sheet100k)
                && TryParseQuadrant(tokens[1], out q50Row, out q50Col)
                && TryParseQuadrant(tokens[2], out int q25Row, out int q25Col)
                && TryParseQuadrant(tokens[3], out int q10Row, out int q10Col)
                && TryRowColFromSerial(sheet100k, columns: 12, out r100, out c100))
            {
                int r50 = (r100 - 1) * 2 + q50Row + 1;
                int c50 = (c100 - 1) * 2 + q50Col + 1;
                int r25 = (r50 - 1) * 2 + q25Row + 1;
                int c25 = (c50 - 1) * 2 + q25Col + 1;
                row = (r25 - 1) * 2 + q10Row + 1;
                col = (c25 - 1) * 2 + q10Col + 1;
                return true;
            }

            return false;
        }

        private static bool TryResolve5k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseQuadrant(tokens[^1], out int qRow, out int qCol))
            {
                return false;
            }

            var parentTokens = tokens.Take(tokens.Count - 1).ToList();
            if (!TryResolve10k(parentTokens, out int r10, out int c10))
            {
                return false;
            }

            row = (r10 - 1) * 2 + qRow + 1;
            col = (c10 - 1) * 2 + qCol + 1;
            return true;
        }

        private static bool TryResolve2k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseSerial(tokens[^1], 1, 9, out int serial9))
            {
                return false;
            }

            var parentTokens = tokens.Take(tokens.Count - 1).ToList();
            if (!TryResolve5k(parentTokens, out int r5, out int c5)
                || !TryRowColFromSerial(serial9, columns: 3, out int r3, out int c3))
            {
                return false;
            }

            row = (r5 - 1) * 3 + r3;
            col = (c5 - 1) * 3 + c3;
            return true;
        }

        private static bool TryResolve1k(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseQuadrant(tokens[^1], out int qRow, out int qCol))
            {
                return false;
            }

            var parentTokens = tokens.Take(tokens.Count - 1).ToList();
            if (!TryResolve2k(parentTokens, out int r2, out int c2))
            {
                return false;
            }

            row = (r2 - 1) * 2 + qRow + 1;
            col = (c2 - 1) * 2 + qCol + 1;
            return true;
        }

        private static bool TryResolve500(IReadOnlyList<string> tokens, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (tokens.Count < 1 || !TryParseQuadrant(tokens[^1], out int qRow, out int qCol))
            {
                return false;
            }

            var parentTokens = tokens.Take(tokens.Count - 1).ToList();
            if (!TryResolve1k(parentTokens, out int r1, out int c1))
            {
                return false;
            }

            row = (r1 - 1) * 2 + qRow + 1;
            col = (c1 - 1) * 2 + qCol + 1;
            return true;
        }

        private static bool TryParseScaleDenominator(string? scale, out int denominator)
        {
            denominator = 0;
            if (string.IsNullOrWhiteSpace(scale))
            {
                return false;
            }

            string text = NormalizeMapNumber(scale)
                .Replace("比例尺", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            Match wan = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*万", RegexOptions.CultureInvariant);
            if (wan.Success
                && double.TryParse(wan.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double wanValue))
            {
                denominator = (int)Math.Round(wanValue * 10_000d, MidpointRounding.AwayFromZero);
                return denominator > 0;
            }

            Match qian = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*千", RegexOptions.CultureInvariant);
            if (qian.Success
                && double.TryParse(qian.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double qianValue))
            {
                denominator = (int)Math.Round(qianValue * 1_000d, MidpointRounding.AwayFromZero);
                return denominator > 0;
            }

            Match ratio = Regex.Match(text, @"[:/](\d[\d,]*)", RegexOptions.CultureInvariant);
            if (ratio.Success)
            {
                string digits = ratio.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
                if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out denominator) && denominator > 0)
                {
                    return true;
                }
            }

            string onlyDigits = Regex.Replace(text, @"[^\d]", string.Empty);
            return int.TryParse(onlyDigits, NumberStyles.None, CultureInfo.InvariantCulture, out denominator)
                   && denominator > 0;
        }

        private static bool TryParseRowLetter(string token, out char rowLetter)
        {
            rowLetter = '\0';
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                char upper = char.ToUpperInvariant(token[0]);
                if (upper is >= 'A' and <= 'V')
                {
                    rowLetter = upper;
                    return true;
                }

                return false;
            }

            if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index is >= 1 and <= 22)
            {
                rowLetter = (char)('A' + index - 1);
                return true;
            }

            return false;
        }

        private static bool TryParseQuadrant(string token, out int rowOffset, out int colOffset)
        {
            rowOffset = 0;
            colOffset = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim().ToUpperInvariant();
            return normalized switch
            {
                "A" or "甲" or "1" or "一" => AssignQuadrant(0, 0, out rowOffset, out colOffset),
                "B" or "乙" or "2" or "二" => AssignQuadrant(0, 1, out rowOffset, out colOffset),
                "C" or "丙" or "3" or "三" => AssignQuadrant(1, 0, out rowOffset, out colOffset),
                "D" or "丁" or "4" or "四" => AssignQuadrant(1, 1, out rowOffset, out colOffset),
                _ => false
            };
        }

        private static bool AssignQuadrant(int row, int col, out int rowOffset, out int colOffset)
        {
            rowOffset = row;
            colOffset = col;
            return true;
        }

        private static bool TryParseSerial(string token, int min, int max, out int serial)
        {
            serial = 0;
            return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out serial)
                   && serial >= min
                   && serial <= max;
        }

        private static bool TryRowColFromSerial(int serial, int columns, out int row, out int col)
        {
            row = ((serial - 1) / columns) + 1;
            col = ((serial - 1) % columns) + 1;
            return true;
        }

        private static string FormatMillionth(char rowLetter, int millionCol)
            => $"{rowLetter}{millionCol:D2}";
    }
}
