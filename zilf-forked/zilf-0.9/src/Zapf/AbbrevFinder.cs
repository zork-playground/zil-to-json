﻿/* Copyright 2010-2018 Jesse McGrew
 * 
 * This file is part of ZILF.
 * 
 * ZILF is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * ZILF is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with ZILF.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
#if DEBUG_ABBREV
using System.Diagnostics;
#endif
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Zilf.Common.StringEncoding;

namespace Zapf
{
    class AbbrevFinder
    {
        public struct Result
        {
            public readonly int Score, Count;
            public readonly string Text;

            public Result(int score, int count, string text)
            {
                Score = score;
                Count = count;
                Text = text;
            }
        }

        struct WordRecord
        {
            public int Savings;
            public Horspool Pattern;
        }

        readonly Dictionary<string, WordRecord> words = new Dictionary<string, WordRecord>();
        StringBuilder allText = new StringBuilder();
        readonly StringEncoder encoder = new StringEncoder();

        /// <summary>
        /// Adds some text to the accumulator.
        /// </summary>
        /// <param name="text">The text to add.</param>
        public void AddText([NotNull] string text)
        {
            allText.Append(text);
            allText.Append('\0');

            foreach (string word in FindWords(text))
            {
                if (!words.ContainsKey(word))
                {
                    var savings = CountSavings(word);
                    if (savings <= 0)
                        continue;

                    words.Add(word, new WordRecord { Savings = savings, Pattern = new Horspool(word) });
                }
            }
        }

        /// <summary>
        /// Gets the number of characters in the accumulator.
        /// </summary>
        public int Position => allText.Length;

        /// <summary>
        /// Rolls the accumulator back to a previous state.
        /// </summary>
        /// <param name="position">The number of characters to keep.</param>
        public void Rollback(int position)
        {
            if (position < allText.Length && position >= 0)
                allText.Length = position;
        }

        static readonly char[] wordDelimiters = { ' ', '.', ',', ':', ';', '!', '?', '(', ')', '/' };

        static IEnumerable<string> FindWords([NotNull] string text)
        {
            int wordStart = -1;
            bool inWord = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int wordEnd;
                if (inWord)
                {
                    if (Array.IndexOf(wordDelimiters, c) >= 0)
                    {
                        inWord = false;
                        wordEnd = i;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    if (c != ' ')
                    {
                        inWord = true;
                        wordStart = i;
                    }

                    continue;
                }

                // found a word
                var word = text.Substring(wordStart, wordEnd - wordStart);
                bool prev = (wordStart > 0), next = (wordEnd < text.Length);

                yield return word;
                if (prev)
                    yield return text[wordStart - 1] + word;
                if (prev & next)
                    yield return text[wordStart - 1] + word + text[wordEnd];
                if (next)
                    yield return word + text[wordEnd];
            }
        }

        int CountSavings(string word)
        {
            encoder.Encode(word, 0, StringEncoderMode.NoAbbreviations, out int zchars);
            return zchars - 2;
        }

        int CountAppearances([NotNull] Horspool pattern)
        {
#if DEBUG_ABBREV
            var stopw = new Stopwatch();
            stopw.Start();
#endif

            int count = 0, index = -1;
            while (true)
            {
                index = pattern.FindIn(allText, index + 1);
                if (index == -1)
                    break;
                count++;
            }

#if DEBUG_ABBREV
            stopw.Stop();
            Console.Error.WriteLine("CountAppearances('{0}') took {1}", pattern.Text, stopw.Elapsed);
#endif

            return count;
        }

        /// <summary>
        /// Returns a sequence of abbreviations and clears the previously added text.
        /// </summary>
        /// <param name="max">The maximum number of abbreviations to return.</param>
        /// <returns>A sequence of abbreviations, in descending order of overall savings.</returns>
        public IEnumerable<Result> GetResults(int max)
        {
            try
            {
                if (max < 1)
                    yield break;

                int numResults = 0;
                while (numResults < max)
                {
#if DEBUG_ABBREV
                    var stopw = new Stopwatch();
                    Console.Error.WriteLine("Querying {0} words in {1} chars of text", words.Count, allText.Length);
                    stopw.Start();
#endif

                    var query =
                        from p in words.AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                        let count = CountAppearances(p.Value.Pattern)
                        let overallSavings = (count - 1) * p.Value.Savings - 2
                        orderby overallSavings descending
                        select new
                        {
                            Savings = overallSavings,
                            Count = count,
                            p.Value.Pattern
                        };

                    var queryResults = query.ToList();

#if DEBUG_ABBREV
                    stopw.Stop();
                    Console.Error.WriteLine("Query time: {0}", stopw.Elapsed);
#endif

                    foreach (var qr in queryResults)
                    {
                        if (qr.Savings <= 0)
                        {
                            words.Remove(qr.Pattern.Text);
                        }
                    }

                    if (words.Count == 0 || queryResults.Count == 0)
                        break;

                    var r = queryResults[0];
                    string word = r.Pattern.Text;
                    yield return new Result(r.Savings, r.Count, word);

                    numResults++;
                    if (numResults >= max)
                        yield break;

                    int idx, lastIdx = 0;
                    while ((idx = r.Pattern.FindIn(allText, lastIdx)) >= 0)
                    {
                        allText.Remove(idx, word.Length);
                        allText.Insert(idx, '\0');
                        lastIdx = idx + 1;
                    }

                    var newText = new StringBuilder(allText.Length);
                    newText.Append(allText);
                    allText = newText;

                    words.Remove(word);
                }
            }
            finally
            {
                words.Clear();
                allText.Length = 0;
                allText.Capacity = 0;
            }
        }
    }
}
