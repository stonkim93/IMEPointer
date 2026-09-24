// KanjiConversion.cs - IMEPointer
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace IMEPointer
{
    /// <summary>
    /// 히라가나 텍스트의 형태소를 분석하여 기본형(어간)과 활용 어미(접미사)를 분리하는 클래스입니다.
    /// 용언(동사, 형용사 등)의 활용을 인식하여 자연스러운 한자 변환을 돕습니다.
    /// </summary>
    public static class JapaneseMorphologyAnalyzer
    {
        // 주요 동사/형용사 활용 어미 목록 (추후 유지보수 시 추가 가능)
        private static readonly string[] Suffixes = 
        {
            "ます", "ました", "ましょう", "ません",
            "ない", "なかった", "なく", "なくて",
            "ている", "ていた", "てる",
            "た", "だ", "だろう", "だった",
            "せる", "させる", "れる", "られる",
            "込む", "込んで", "込んだ",
            "する", "した", "して",
            "かった", "くて"
        };

        /// <summary>
        /// 주어진 히라가나 문자열을 분석하여 가능한 어간(BaseForm)과 어미(Suffix) 조합들을 반환합니다.
        /// </summary>
        /// <param name="hiragana">분석할 히라가나 문자열</param>
        /// <returns>기본형(어간)과 접미사 튜플의 리스트</returns>
        public static List<(string BaseForm, string OriginalSuffix)> Analyze(string hiragana)
        {
            var results = new List<(string BaseForm, string OriginalSuffix)> { (hiragana, "") };
            if (string.IsNullOrEmpty(hiragana)) return results;

            foreach (string suffix in Suffixes)
            {
                if (hiragana.EndsWith(suffix) && hiragana.Length > suffix.Length)
                {
                    string stem = hiragana.Substring(0, hiragana.Length - suffix.Length);
                    if (stem.Length == 0) continue;
                    
                    char lastChar = stem[stem.Length - 1];
                    char s0 = suffix[0]; 

                    if (s0 == 'し' || s0 == 'す' || s0 == 'さ') 
                    {
                        results.Add((stem, suffix));
                    }

                    if (CharacterDatabase.IsValidCharacter(lastChar))
                    {
                        ushort code = CharacterDatabase.GetCodeFromChar(lastChar);
                        string baseStem = stem.Substring(0, stem.Length - 1);

                        if (s0 == 'た' || s0 == 'だ')
                        {
                            switch (code)
                            {
                                case 332: // っ
                                    results.Add((baseStem + "う", "っ" + suffix));
                                    results.Add((baseStem + "つ", "っ" + suffix));
                                    results.Add((baseStem + "る", "っ" + suffix));
                                    break;
                                case 092: // ん
                                    results.Add((baseStem + "む", "ん" + suffix));
                                    results.Add((baseStem + "ぬ", "ん" + suffix));
                                    results.Add((baseStem + "ぶ", "ん" + suffix));
                                    break;
                                case 001: // い
                                    results.Add((baseStem + "く", "い" + suffix));
                                    results.Add((baseStem + "ぐ", "い" + suffix));
                                    break;
                            }
                        }

                        if (code < 200 && (code % 10) == 1) // i-row
                        {
                            ushort uCode = (ushort)(code + 1); // u-row
                            if (CharacterDatabase.ContainsCode(uCode))
                            {
                                char uChar = CharacterDatabase.GetCharacterData(uCode).Hiragana;
                                results.Add((baseStem + uChar, lastChar + suffix));
                            }
                        }
                    }
                }
            }
            return results;
        }
    }

    /// <summary>
    /// 동적 계획법(DP) 및 비터비(Viterbi) 알고리즘을 활용하여 
    /// 입력된 히라가나 문자열을 최적의 한자 후보들로 변환하는 클래스입니다.
    /// </summary>
    public static class KanjiConverter
    {
        public struct DpNode
        {
            public ushort RightId;
            public int Cost;
            public string Kanji;
        }

        /// <summary>
        /// 히라가나 입력 문자열에 대해 가장 적합한 한자 변환 후보(KanjiEntry) 리스트를 추출합니다.
        /// 형태소 분석 및 형태소 간 연결 비용(Transition Cost)을 고려하여 최적 경로를 찾습니다.
        /// </summary>
        /// <param name="text">변환할 원본 문자열</param>
        /// <returns>우선순위(Cost)가 높은 순서대로 정렬된 한자 후보 리스트</returns>
        public static List<MozcDictionary.KanjiEntry> GetKanjiCandidatesOptimized(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) 
                return new List<MozcDictionary.KanjiEntry>();

            string normalized = JapaneseCharacterProcessor.ToHiragana(text);
            int n = normalized.Length;
            
            // 메모리 최적화: Substring 길이 상한을 20에서 10으로 줄여 쓰레기 객체 생성 급감
            int maxSearchLen = 10;
            var allSubstrings = new HashSet<string>();
            for (int i = 0; i < n; i++) {
                int maxLen = Math.Min(maxSearchLen, n - i);
                for (int len = 1; len <= maxLen; len++) {
                    allSubstrings.Add(normalized.Substring(i, len));
                }
            }
            var dictMatches = MozcDictionary.GetEntriesForReadingsBatch(allSubstrings);

            int beamWidth = 20; // DP 가지치기 최적화 
            var dp = new List<DpNode>[n + 1];
            for (int i = 0; i <= n; i++) 
                dp[i] = new List<DpNode>(beamWidth * 2);
            
            dp[0].Add(new DpNode { RightId = 0, Cost = 0, Kanji = "" });

            for (int i = 0; i < n; i++)
            {
                if (dp[i].Count == 0) continue;

                // Beam Search: 정렬 및 가지치기 (Dictionary 할당 제거)
                var currentNodes = dp[i]
                    .GroupBy(x => x.Kanji)
                    .Select(g => g.OrderBy(x => x.Cost).First())
                    .OrderBy(x => x.Cost)
                    .Take(beamWidth)
                    .ToList();
                
                dp[i] = currentNodes; // 최적화된 노드 리스트로 교체

                var matches = new List<MozcDictionary.ReadingMatch>();
                int maxLen = Math.Min(maxSearchLen, n - i);
                for (int len = 1; len <= maxLen; len++)
                {
                    string subText = normalized.Substring(i, len);
                    
                    if (dictMatches.TryGetValue(subText, out var entries)) {
                        foreach (var e in entries) 
                            matches.Add(new MozcDictionary.ReadingMatch { Length = len, Entry = e });
                    }

                    if (len > 1) {
                        var morphs = JapaneseMorphologyAnalyzer.Analyze(subText);
                        foreach (var morph in morphs) {
                            if (string.IsNullOrEmpty(morph.OriginalSuffix)) continue;
                            if (dictMatches.TryGetValue(morph.BaseForm, out var baseEntries)) {
                                foreach (var baseEntry in baseEntries) {
                                    var combinedKanji = baseEntry.Kanji + morph.OriginalSuffix;
                                    matches.Add(new MozcDictionary.ReadingMatch { 
                                        Length = len, 
                                        Entry = new MozcDictionary.KanjiEntry(subText, combinedKanji, baseEntry.LeftId, baseEntry.RightId, baseEntry.Cost + 500) 
                                    });
                                }
                            }
                        }
                    }

                    if (len == 1)
                    {
                        ushort fallbackId = 1934;
                        int fallbackCost = CharacterDatabase.IsValidCharacter(subText[0]) ? 8000 : 10000;
                        
                        matches.Add(new MozcDictionary.ReadingMatch {
                            Length = 1,
                            Entry = new MozcDictionary.KanjiEntry(subText, subText, fallbackId, fallbackId, fallbackCost) 
                        });
                    }
                }

                foreach (var match in matches)
                {
                    int nextIdx = i + match.Length;
                    if (nextIdx > n) continue;

                    var cand = match.Entry;
                    foreach (var path in currentNodes)
                    {
                        int transitionCost = MozcDictionary.GetTransitionCost(path.RightId, cand.LeftId);
                        int totalCost = path.Cost + transitionCost + cand.Cost;

                        dp[nextIdx].Add(new DpNode { RightId = cand.RightId, Cost = totalCost, Kanji = path.Kanji + cand.Kanji });
                    }
                }
            }

            var finalPaths = dp[n]
                .Select(path => new { path.Kanji, Cost = path.Cost + MozcDictionary.GetTransitionCost(path.RightId, 0) })
                .OrderBy(p => p.Cost)
                .GroupBy(p => p.Kanji)
                .Select(g => g.First())
                .Take(9)
                .ToList();

            var results = new List<MozcDictionary.KanjiEntry>();
            foreach (var path in finalPaths) {
                results.Add(new MozcDictionary.KanjiEntry(normalized, path.Kanji, 0, 0, path.Cost));
            }
            return results;
        }
    }
}
