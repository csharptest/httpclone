#region Copyright 2011 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Analysis;
using Lucene.Net.Store;
using Lucene.Net.Analysis.Standard;
using Version = Lucene.Net.Util.Version;
using System.Globalization;

namespace CSharpTest.Net.HttpClone.Storage
{
    public partial class ContentStorage
	{
        private IndexReader _search;
        private Analyzer _analyzer;

        public IEnumerable<ContentSearchResult> Similar(string uriExisting, int limit, bool newest)
        {
            var docs = IndexReader.TermDocs(new Term("uri", uriExisting));
            if(docs.Next())
            {
                int docid = docs.Doc();
                Document info = IndexReader.Document(docid);
                string title = info.GetField("title").StringValue();

                TermFreqVector[] terms = IndexReader.GetTermFreqVectors(docid);
                if (terms.Length == 1)
                {
                    List<TermQuery> find = new List<TermQuery>();
                    int[] freq = terms[0].GetTermFrequencies();
                    int limitfreq = Math.Max(10, (int)(freq.Max()*0.5));
                    string[] word = terms[0].GetTerms();
                    for (int i = 0; i < freq.Length && i < word.Length; i++)
                    {
                        if (freq[i] > 1 && freq[i] < limitfreq)
                            find.Add(new TermQuery(new Term("contents", word[i])));
                    }
                    
                    TopDocsCollector collector = !newest
                                                     ? (TopDocsCollector) TopScoreDocCollector.create(limit, true)
                                                     : (TopDocsCollector) TopFieldCollector.create(
                                                         new Sort(new SortField("modified", SortField.STRING, true)),
                                                         limit, true, true, true, true);

                    Searcher searcher;
                    if (RunQuery(new BooleanQuery().Combine(find.ToArray()), out searcher, collector))
                    {
                        return EnumSearchResults(0, searcher, collector, limit + 1)
                            .Where(r => r.Uri != uriExisting).Take(limit);
                    }
                }
            }

            return new ContentSearchResult[0];
        }

	    public IEnumerable<ContentSearchResult> Search(string searchTerm, int start, int count, bool newest, out int total)
        {
            int limit = start + count;
            Searcher searcher;
            TopDocsCollector collector = !newest
                ? (TopDocsCollector)TopScoreDocCollector.create(limit, true)
                : (TopDocsCollector)TopFieldCollector.create(
                        new Sort(new SortField("modified", SortField.STRING, true)),
                        limit, true, true, true, true);

            if (RunQuery(searchTerm, out searcher, collector))
            {
                total = collector.GetTotalHits();
                return EnumSearchResults(start, searcher, collector, limit);
            }

            total = 0;
            return new ContentSearchResult[0];
        }

	    private IEnumerable<ContentSearchResult> EnumSearchResults(int start, Searcher searcher, TopDocsCollector collector, int limit)
	    {
	        TopDocs results = collector.TopDocs();
	        float max = results.GetMaxScore();
	        ScoreDoc[] found = results.scoreDocs;
	        limit = Math.Min(limit, found.Length);

	        for (int i = start; i < limit; i++)
	        {
	            ScoreDoc doc = found[i];
	            Document docInfo = searcher.Doc(doc.doc);

	            ContentSearchResult.Builder builder = new ContentSearchResult.Builder();
	            builder.SetRanking((uint) Math.Max(0, Math.Min(100, (int) (doc.score/max*100f))));
	            builder.SetUri(docInfo.GetField("uri").StringValue());
	            builder.SetTitle(docInfo.GetField("title").StringValue());
	            builder.SetBlurb(docInfo.GetField("blurb").StringValue());
	            builder.SetModified(DateTime.ParseExact(docInfo.GetField("modified").StringValue(),
	                                                    "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
	                                                    DateTimeStyles.None));
	            ContentRecord record;
	            if (TryGetValue(builder.Uri, out record))
	            {
	                builder.SetRecord(record);
	            }

	            yield return builder.Build();
	        }
	    }

        private Analyzer Analyzer
        {
            get { return _analyzer ?? (_analyzer = new StandardAnalyzer(Version.LUCENE_29)); }
        }

	    private IndexReader IndexReader
	    {
	        get
	        {
                if (_search == null)
                    _disposables.Add(new DisposingIndex(_search = IndexReader.Open(FSDirectory.Open(new DirectoryInfo(IndexDirectory)), true)));
	            return _search;
	        }
	    }

        private bool RunQuery(string searchTerm, out Searcher searcher, Collector collector)
        {
            return RunQuery(CreateQuery("contents", searchTerm), out searcher, collector);
        }

        private bool RunQuery(Query query, out Searcher searcher, Collector collector)
        {
            try
            {
                searcher = new IndexSearcher(IndexReader);
                searcher.Search(query, collector);
                return true;
            }
            catch
            {
                searcher = null;
                return false;
            }
        }

        private Query CreateQuery(string field, string term)
        {
            QueryParser parser = new QueryParser(Version.LUCENE_29, field, Analyzer);

            parser.SetFuzzyMinSim(0.70f);
            parser.SetFuzzyPrefixLength(3);
            parser.SetDefaultOperator(QueryParser.Operator.OR);
            parser.SetAllowLeadingWildcard(true);

            return parser.Parse(term);
        }

	    private class DisposingIndex : IDisposable
	    {
	        private readonly IndexReader _idx;

	        public DisposingIndex(IndexReader idx)
            {
                _idx = idx;
            }

            ~DisposingIndex()
            {
                try { _idx.Close(); }
                catch { return; }
            }

	        void IDisposable.Dispose()
	        {
	            GC.SuppressFinalize(this);
                _idx.Close();
            }
        }
	}
}
