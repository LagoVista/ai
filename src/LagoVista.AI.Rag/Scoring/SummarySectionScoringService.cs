using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LagoVista.AI.Rag.Scoring
{
    /// <summary>
    /// Default deterministic implementation of ISummarySectionScoringService.
    /// Uses simple, explainable heuristics over structure, domain vocabulary,
    /// and noise to compute per-dimension and composite scores.
    /// </summary>
    public sealed class SummarySectionScoringService : ISummarySectionScoringService
    {
        private readonly IReadOnlyList<GlobalModelDescriptor> _globalModels;
        private readonly SummarySectionScoringOptions _options;

        public SummarySectionScoringService(
            IEnumerable<GlobalModelDescriptor> globalModels,
            SummarySectionScoringOptions options)
        {
            _globalModels = (globalModels ?? Array.Empty<GlobalModelDescriptor>()).ToList();
            _options = options ?? new SummarySectionScoringOptions();
        }

        public SummarySectionScoreResult Score(SummarySectionScoreRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new SummarySectionScoreResult
            {
                SnippetId = request.SnippetId,
                SubtypeKind = request.SubtypeKind
            };

            var text = request.Text ?? string.Empty;
            var trimmed = text.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                result.CompositeScore = 0;
                result.Category = SummarySectionScoreCategory.Reject;
                result.Flags.Add("EmptyText");
                result.Reasons.Add("Snippet text is empty or whitespace.");
                return result;
            }

            var tokens = Tokenize(trimmed).ToList();
            var charCount = trimmed.Length;
            var lineCount = trimmed.Split(new[] { '\n' }, StringSplitOptions.None).Length;
            var sentenceCount = CountSentences(trimmed);

            var dimensionScores = new Dictionary<ScoreDimension, double>();
            var flags = new List<string>();
            var reasons = new List<string>();

            var structural = ScoreStructuralClarity(trimmed, charCount, lineCount, sentenceCount, flags, reasons);
            dimensionScores[ScoreDimension.StructuralClarity] = structural;

            var cohesion = ScoreSemanticCohesion(tokens, flags, reasons);
            dimensionScores[ScoreDimension.SemanticCohesion] = cohesion;

            var matchedModels = FindMatchedModels(trimmed);
            foreach (var match in matchedModels)
            {
                result.MatchedModels.Add(match);
            }

            var domainAnchoring = ScoreDomainAnchoring(matchedModels, flags, reasons);
            dimensionScores[ScoreDimension.DomainAnchoring] = domainAnchoring;

            var noise = ScoreNoiseRatio(trimmed, tokens, flags, reasons);
            dimensionScores[ScoreDimension.NoiseRatio] = noise;

            var coverage = ScoreCoverage(trimmed, sentenceCount, matchedModels, tokens, flags, reasons);
            dimensionScores[ScoreDimension.Coverage] = coverage;

            var queryAlignment = ScoreQueryAlignment(tokens, flags, reasons);
            dimensionScores[ScoreDimension.QueryAlignment] = queryAlignment;

            result.DimensionScores = dimensionScores;
            result.Flags = flags;
            result.Reasons = reasons;

            var composite =
                structural * _options.StructuralClarityWeight +
                cohesion * _options.SemanticCohesionWeight +
                domainAnchoring * _options.DomainAnchoringWeight +
                noise * _options.NoiseRatioWeight +
                coverage * _options.CoverageWeight +
                queryAlignment * _options.QueryAlignmentWeight;

            result.CompositeScore = Math.Max(0, Math.Min(100, composite));
            result.Category = Categorize(result.CompositeScore);

            return result;
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            // Simple tokenization on whitespace; punctuation is handled separately.
            return text
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim());
        }

        private static int CountSentences(string text)
        {
            // Very simple heuristic: count ., !, ? as sentence terminators.
            var count = 0;
            foreach (var ch in text)
            {
                if (ch == '.' || ch == '!' || ch == '?')
                {
                    count++;
                }
            }

            return count == 0 ? 1 : count;
        }

        private double ScoreStructuralClarity(
            string text,
            int charCount,
            int lineCount,
            int sentenceCount,
            IList<string> flags,
            IList<string> reasons)
        {
            var score = 100.0;

            if (charCount < 40)
            {
                score -= 40;
                flags.Add("VeryShortText");
                reasons.Add("Text is very short; likely under-explained.");
            }

            if (sentenceCount <= 1 && charCount > 120)
            {
                score -= 15;
                flags.Add("SingleLongSentence");
                reasons.Add("Text is one long sentence; consider splitting into multiple sentences.");
            }

            if (lineCount == 1 && charCount > 120)
            {
                score -= 10;
                flags.Add("SingleLineBlock");
                reasons.Add("Text is a single long line; consider line breaks for readability.");
            }

            var codeChars = new[] { '{', '}', ';', '(', ')', '[', ']', '<', '>' };
            var codeCharCount = text.Count(c => codeChars.Contains(c));
            if (charCount > 0)
            {
                var ratio = (double)codeCharCount / charCount;
                if (ratio > 0.15)
                {
                    score -= 25;
                    flags.Add("CodeLikeStructure");
                    reasons.Add("Text appears to contain a large amount of code-like characters.");
                }
            }

            return Clamp(score);
        }

        private double ScoreSemanticCohesion(
            IList<string> tokens,
            IList<string> flags,
            IList<string> reasons)
        {
            var words = tokens
                .Select(t => Regex.Replace(t.ToLowerInvariant(), "[^a-z]", string.Empty))
                .Where(t => t.Length >= 4)
                .ToList();

            if (words.Count == 0)
            {
                flags.Add("NoContentWords");
                reasons.Add("Unable to find substantive words for cohesion analysis.");
                return 60.0; // neutral-ish
            }

            var total = words.Count;
            var distinct = words.Distinct().Count();
            var ratio = (double)distinct / total;

            // If every word is unique, text may be rambling or multi-topic.
            if (ratio > 0.7)
            {
                flags.Add("HighWordDiversity");
                reasons.Add("Many unique words with little repetition; may indicate multiple topics.");
                return 55.0;
            }

            if (ratio > 0.4)
            {
                return 75.0;
            }

            return 90.0;
        }

        private IList<MatchedModelInfo> FindMatchedModels(string text)
        {
            var matches = new List<MatchedModelInfo>();

            if (_globalModels.Count == 0)
            {
                return matches;
            }

            var lower = text.ToLowerInvariant();

            foreach (var model in _globalModels)
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    continue;
                }

                var nameLower = model.Name.ToLowerInvariant();

                // Basic word-boundary match to avoid accidental substrings.
                var pattern = $"\\b{nameLower}\\b";
                if (Regex.IsMatch(lower, pattern))
                {
                    matches.Add(new MatchedModelInfo
                    {
                        Name = model.Name,
                        Domain = model.Domain,
                        Description = model.Description
                    });
                }
            }

            return matches;
        }

        private double ScoreDomainAnchoring(
            IList<MatchedModelInfo> matchedModels,
            IList<string> flags,
            IList<string> reasons)
        {
            if (matchedModels == null || matchedModels.Count == 0)
            {
                flags.Add("NoDomainModelsDetected");
                reasons.Add("No known domain models were detected in the text.");
                return 40.0;
            }

            if (matchedModels.Count == 1)
            {
                return 80.0;
            }

            return 95.0;
        }

        private double ScoreNoiseRatio(
            string text,
            IList<string> tokens,
            IList<string> flags,
            IList<string> reasons)
        {
            if (tokens.Count == 0)
            {
                return 70.0;
            }

            var noiseTokens = 0;
            foreach (var token in tokens)
            {
                if (token.Contains("//") || token.Contains("/*") || token.Contains("*/"))
                {
                    noiseTokens++;
                    continue;
                }

                if (token.Any(char.IsDigit))
                {
                    noiseTokens++;
                    continue;
                }

                if (token.Contains(";") || token.Contains("=>") || token.Contains("namespace") || token.Contains("class"))
                {
                    noiseTokens++;
                    continue;
                }
            }

            var ratio = (double)noiseTokens / tokens.Count;
            var score = (1.0 - ratio) * 100.0;

            if (ratio > 0.3)
            {
                flags.Add("HighNoise");
                reasons.Add("A significant portion of tokens look like noise or code artifacts.");
            }

            return Clamp(score);
        }

        private double ScoreCoverage(
            string text,
            int sentenceCount,
            IList<MatchedModelInfo> matchedModels,
            IList<string> tokens,
            IList<string> flags,
            IList<string> reasons)
        {
            var score = 40.0;
            var length = text.Length;

            if (length >= 80)
            {
                score += 15;
            }

            if (length >= 160)
            {
                score += 15;
            }

            if (sentenceCount >= 2)
            {
                score += 10;
            }

            if (matchedModels != null && matchedModels.Count > 0)
            {
                score += 10;
            }

            if (ContainsAny(tokens, _options.DomainVerbs))
            {
                score += 10;
            }

            if (score < 60)
            {
                flags.Add("LowCoverage");
                reasons.Add("Text may not provide enough context (what/why/how) to stand alone.");
            }

            return Clamp(score);
        }

        private double ScoreQueryAlignment(
            IList<string> tokens,
            IList<string> flags,
            IList<string> reasons)
        {
            var score = 50.0;

            if (ContainsAny(tokens, _options.DomainVerbs))
            {
                score += 15;
            }

            if (ContainsAny(tokens, _options.RoleKeywords))
            {
                score += 15;
            }

            if (score < 60)
            {
                flags.Add("LowQueryAlignment");
                reasons.Add("Snippet text may not align well with expected query verbs or roles.");
            }

            return Clamp(score);
        }

        private static bool ContainsAny(IList<string> tokens, IList<string> candidates)
        {
            if (tokens == null || tokens.Count == 0 || candidates == null || candidates.Count == 0)
            {
                return false;
            }

            var tokenSet = new HashSet<string>(tokens.Select(t => t.Trim().ToLowerInvariant()));

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                var candidateLower = candidate.Trim().ToLowerInvariant();
                if (tokenSet.Contains(candidateLower))
                {
                    return true;
                }
            }

            return false;
        }

        private static double Clamp(double value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private static SummarySectionScoreCategory Categorize(double compositeScore)
        {
            if (compositeScore >= 85) return SummarySectionScoreCategory.Excellent;
            if (compositeScore >= 70) return SummarySectionScoreCategory.Good;
            if (compositeScore >= 60) return SummarySectionScoreCategory.Fair;
            if (compositeScore >= 40) return SummarySectionScoreCategory.Poor;
            return SummarySectionScoreCategory.Reject;
        }
    }
}
