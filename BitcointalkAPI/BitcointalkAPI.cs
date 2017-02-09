using BitcointalkAPI.Utilities;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BitcointalkAPI
{
    
    /// <summary>
    /// Bitcointalk board
    /// </summary>
    public class Board
    {
        /// <summary>
        /// Settings for fetching web pages within the Board object
        /// </summary>
        public WebConfig webSettings;
        /// <summary>
        /// Event, called every time a Bitcointalk board page is processed
        /// </summary>
        public event EventHandler<PageScannedEventArgs<Topic>> PageScanned = delegate { };
        /// <summary>
        /// Event, called when the processing of a Bitcointalk board page fails
        /// </summary>
        public event EventHandler<ScannerFailureEventArgs> ScanFailure = delegate { };
        /// <summary>
        /// The ID of the Bitcointalk board
        /// </summary>
        protected string boardId;
        /// <summary>
        /// Constant for constructing the full link to the Bitcointalk board
        /// </summary>
        protected const string boardPrefix = "https://bitcointalk.org/index.php?board=", boardPostfix = ".0";
        /// <summary>
        /// Number of times the Board object will attempt to fetch a Bitcointalk page
        /// </summary>
        protected const int restartLimit = 5;
        /// <summary>
        /// URL to the Bitcointalk board's first page
        /// </summary>
        public string Link
        {
            get
            {
                return boardPrefix + boardId + boardPostfix;
            }
            protected set
            {
                if (!Regex.IsMatch(value, @"^https://bitcointalk\.org/index\.php\?board=(\d+)\.(\d+)$"))
                {
                    throw new InvalidBitcointalkInputException("link doesn't match board pattern", typeof(Board));
                }
                else
                {
                    boardId = Regex.Replace(value.Replace(boardPrefix, ""), @"\.(\d+)$", "");
                }
            }
        }


        /// <summary>
        /// Create a new Board objects
        /// </summary>
        /// <param name="boardLink">URL of the first Bitcointalk board page</param>
        /// <param name="webSettings">Settings for fetching pages from Bitcointalk</param>
        public Board(string boardLink, WebConfig webSettings)
        {
            Link = boardLink;
            this.webSettings = webSettings;
        }


        /// <summary>
        /// Get the topics/threads from a selected number of Bitcointalk board pages
        /// </summary>
        /// <param name="boardPage">The first board page from which to collect the topics/threads (not less than 1)</param>
        /// <param name="pagesToScan">Number of Bitcointalk board pages to scan</param>
        /// <param name="token">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A collection of topics/threads</returns>
        public ICollection<Topic> GetTopics(int boardPage, int pagesToScan = 1, CancellationToken token = default(CancellationToken))
        {
            return Task.Run(() => { return GetTopicsAsync(boardPage, pagesToScan, token); }).Result;
        }

        /// <summary>
        /// Get the topics/threads from a selected number of Bitcointalk board pages asynchronously 
        /// </summary>
        /// <param name="boardPage">The first board page from which to collect the topics/threads (not less than 1)</param>
        /// <param name="pagesToScan">Number of Bitcointalk board pages to scan</param>
        /// <param name="token">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A collection of topics/threads</returns>
        public async Task<ICollection<Topic>> GetTopicsAsync(int boardPage, int pagesToScan = 1, CancellationToken token = default(CancellationToken))
        {
            HashSet<Topic> topicList = new HashSet<Topic>(); //All links to topics

            if (boardPage < 1)
            {
                boardPage = 1;
            }

            //Go through [pagesToScan] of board pages looking for topics
            for (int currentPage = boardPage; currentPage < boardPage + pagesToScan; currentPage++)
            {
                for (int counter = 0; counter < restartLimit; counter++)
                {
                    try
                    {
                        topicList.UnionWith(ScanBoardPage(currentPage));
                        await Task.Delay(webSettings.requestDelay);
                        PageScanned(this, new PageScannedEventArgs<Topic>(currentPage, (currentPage - boardPage) / (float)pagesToScan));
                        break;
                    }
                    catch (WebException)
                    {
                        token.CancelIfRequestedAndNotDefault();
                        ScanFailure(this, new ScannerFailureEventArgs((counter == restartLimit - 1 ? "Last attempt: " : "Attempt #" + (counter + 1) + ":") +
                            "Download of board page #" + currentPage + " failed. Waiting and attempting to fetch page again."));
                        await Task.Delay(webSettings.requestDelay * counter);
                    }
                }

                token.CancelIfRequestedAndNotDefault();
            }

            return topicList;
        }


        public override bool Equals(object obj)
        {
            try
            {
                if (((Board)obj).Link == this.Link)
                {
                    return true;
                }

            }
            catch (InvalidCastException)
            {
                return false;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Link.GetHashCode();
        }

        /// <summary>
        /// Scan a selected board page for topics
        /// </summary>
        /// <param name="currentPage">The board page to scan (starting with 1)</param>
        /// <returns>A collection of topics/threads</returns>
        protected ICollection<Topic> ScanBoardPage(int currentPage)
        {
            HashSet<Topic> topicList = new HashSet<Topic>();
            string boardUrl = Link.Remove(Link.Length-boardPostfix.Length+1) + ((currentPage - 1) * 40).ToString();

            string html;
            using (WebClient downloader = new WebClient())
            {
                if (webSettings.proxyLink != default(string))
                {
                    downloader.Proxy = new WebProxy(webSettings.proxyLink);
                }

                html = downloader.DownloadString(boardUrl);
            }

            HtmlDocument parsedHtml = new HtmlDocument();
            parsedHtml.LoadHtml(html);
            
            // Select all linkcontainers from a board page and create extra links for the last [threadPages]
            try
            {
                foreach (HtmlNode data in parsedHtml.DocumentNode.SelectNodes(@"//small"))
                {
                    bool canUseAllFunction = false;
                    string selectedLink = ""; // Link currently selected for adding to list

                    // Checks to see if link container node isn't empty
                    if (data.SelectSingleNode(@"a[@href]") == null)
                    {
                        selectedLink = data.ParentNode.SelectSingleNode(@"span/a[@href]").GetAttributeValue("href", "");
                        canUseAllFunction = true;
                    }
                    else
                    {

                        //Selects all number page links in link container
                        foreach (HtmlNode linkData in data.SelectNodes(@"a[@href]"))
                        {
                            string link = linkData.GetAttributeValue("href", "");

                            //Cleaning up results (prevents non-topic links when board isn't on the deepest level)
                            if (!link.Contains("topic="))
                            {
                                continue;
                            }

                            if (Regex.IsMatch(link, @";all$"))
                            {
                                canUseAllFunction = true;
                                link = Regex.Replace(link, @";all$", "");
                            }


                            if (selectedLink == "")
                            {
                                selectedLink = link;
                            }
                            else if (int.Parse(Regex.Match(link, @"(\d+)$").Value) > int.Parse(Regex.Match(selectedLink, @"(\d+)$").Value))
                            {
                                selectedLink = link;
                            }
                        }


                        // Checks if a thread doesn't have multiple pages and selects the core URL if it doesn't
                        if (selectedLink == "")
                        {
                            foreach (HtmlNode link in data.ParentNode.SelectNodes(@"span[@id]/a[@href]"))
                            {
                                if (!link.GetAttributeValue("href", "").Contains("topic="))
                                {
                                    continue;
                                }
                                selectedLink = link.GetAttributeValue("href", "");
                            }
                        }

                    }

                    if (selectedLink != "")
                    {
                        try
                        {
                            topicList.Add(new Topic(selectedLink, new PostParser(webSettings), canUseAllFunction, (int.Parse(Regex.Match(selectedLink, @"(\d+)$").Value) / 20) + 1, webSettings));
                        }
                        catch (Exception)
                        {
                            topicList.Add(new Topic(selectedLink, new PostParser(webSettings), canUseAllFunction, 1, webSettings));
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw new InvalidBitcointalkInputException("Can't parse one of the board's pages", typeof(Board));
            }
                     
            return topicList;
        }

    }

    /// <summary>
    /// Bitcointalk topic / thread
    /// </summary>
    public class Topic
    {
        /// <summary>
        /// Number of pages the topic / thread has
        /// </summary>
        public int maxPages;
        /// <summary>
        /// Event, called whenever a topic / thread page is processed
        /// </summary>
        public event EventHandler<PageScannedEventArgs<Post>> PageScanned = delegate { };
        /// <summary>
        /// Event, called whenever the processing of a topic / thread page fails
        /// </summary>
        public event EventHandler<ScannerFailureEventArgs> ScanFailure = delegate { };
        /// <summary>
        /// Settings for fetching web pages within the Topic object
        /// </summary>
        public WebConfig webSettings;
        /// <summary>
        /// Text used to replace all smiley emoticons in posts found within the topic.
        /// </summary>
        public string smileyReplacer = ",";

        /// <summary>
        /// A cache of all posts belonging to the Topic that have been fetched
        /// </summary>
        protected Dictionary<int, Post[]> postsIndexed = new Dictionary<int, Post[]>();
        /// <summary>
        /// The Id of the topic
        /// </summary>
        protected string topicID;
        /// <summary>
        /// Indicates whether the cache contains all posts within the topic (works when fetched using the 'all posts' or 'print' function)
        /// </summary>
        protected bool hasAllPosts = false;
        /// <summary>
        /// Indicates whether it's possible to fetch all posts from the topic at once
        /// </summary>
        protected bool canUseAllFunction = false;
        /// <summary>
        /// Constants for constructing the topic URL
        /// </summary>
        protected const string topicPrefix = "https://bitcointalk.org/index.php?topic=", topicPostFix = ".0";
        /// <summary>
        /// Number of times the Topic object will attempt to fetch a Bitcointalk page
        /// </summary>
        protected const int restartLimit = 5;
        /// <summary>
        /// The object responsible for parsing the HTML of topic pages
        /// </summary>
        protected readonly PostParser topicPageParser;

        /// <summary>
        /// The URL to the topic's / thread's first page
        /// </summary>
        public string Link
        {
            get
            {
                if (topicID == default(string))
                {
                    return default(string);
                }
                else
                {
                    return topicPrefix + topicID + topicPostFix;
                }
            }
            protected set
            {
                if (!Regex.IsMatch(value,@"^https://bitcointalk\.org/index\.php\?topic=(\d+)\.(\d+)$"))
                {
                    throw new InvalidBitcointalkInputException("link doesn't match topic pattern",typeof(Topic));
                }
                else
                {
                    topicID = Regex.Replace(value.Replace(topicPrefix, ""), @"\.(\d+)$", "");
                }
            }
        }
        /// <summary>
        /// The number of pages the topic / thread has (may require Internet connection to retrieve it)
        /// </summary>
        public int MaxPages
        {
            get
            {
                if (maxPages == default(int))
                {
                    GetMaxPages();
                }
                return maxPages;
            }
            protected set
            {
                if (value < 0)
                {
                    maxPages = 1;
                }
                else
                {
                    maxPages = value;
                }
            }
        }
        /// <summary>
        /// The ID of the topic / thread
        /// </summary>
        public string ID
        {
            get
            {
                return topicID;
            }
        }


        /// <summary>
        /// Forced creation of a Topic (make sure the paramaters are valid)
        /// </summary>
        /// <param name="topicLink">URL to the topic's page (any)</param>
        /// <param name="postParser">The object responsible for parsing the topic's HTML pages</param>
        /// <param name="canUseAllFunction">Can the topic use the 'all pages' function</param>
        /// <param name="maxPages">The number of pages the topic has</param>
        /// <param name="webSettings">The settings for fetching web pages</param>
        internal Topic(string topicLink, PostParser postParser, bool canUseAllFunction ,int maxPages, WebConfig webSettings)
        {
            Link = topicLink;
            MaxPages = maxPages;
            topicPageParser = postParser;
            this.canUseAllFunction = canUseAllFunction;
            this.webSettings = webSettings;
        }

        /// <summary>
        /// Create a new Topic object
        /// </summary>
        /// <param name="topicLink">URL to the topic's / thread's page (any)</param>
        /// <param name="webSettings">The settings for fetching web pages</param>
        /// <param name="postParser">The object responsible for parsing the topic's / thread's HTML pages</param>
        public Topic(string topicLink, WebConfig webSettings, PostParser postParser = default(PostParser))
        {
            Link = topicLink;
            if (postParser != default(PostParser))
            {
                topicPageParser = postParser;
            }
            else
            {
                topicPageParser = new PostParser(webSettings);
            }
            this.webSettings = webSettings;
        }


        /// <summary>
        /// Get posts from a selected number of topic / thread pages
        /// </summary>
        /// <param name="topicPage">The page number of the first page you want posts from (starting with 1)</param>
        /// <param name="pagesToScan">The number of pages you want to collect</param>
        /// <param name="cancelToken">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A collection of posts from the selected number of pages</returns>
        public ICollection<Post> GetPosts(int topicPage, int pagesToScan = 1, CancellationToken cancelToken = default(CancellationToken))
        {
            return Task.Run(() => { return GetPostsAsync(topicPage, pagesToScan, cancelToken); }).Result;
        }

        /// <summary>
        /// Get posts from a selected number of topic / thread pages asynchronously
        /// </summary>
        /// <param name="topicPage">The page number of the first page you want posts from (starting with 1)</param>
        /// <param name="pagesToScan">The number of pages you want to collect</param>
        /// <param name="cancelToken">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A collection of posts from the selected number of pages</returns>
        public async Task<ICollection<Post>> GetPostsAsync(int topicPage, int pagesToScan = 1, CancellationToken cancelToken = default(CancellationToken))
        {
            HashSet<Post> postList = new HashSet<Post>();
            
            if (topicPage < 1)
            {
                topicPage = 1;
            }

            if (topicPage + pagesToScan - 1 > MaxPages)
            {
                pagesToScan = MaxPages - topicPage + 1;
                if (pagesToScan < 0)
                {
                    pagesToScan = 0;
                }
            }

            for (int currentPage = topicPage; currentPage < topicPage + pagesToScan; currentPage++)
            {
                if (postsIndexed.ContainsKey(currentPage))
                {
                    postList.UnionWith(postsIndexed[currentPage]);
                }
                else
                {

                    for (int counter = 0; counter < restartLimit; counter++)
                    {
                        try
                        {
                            ICollection<Post> postsInPage = ScanTopicPage(currentPage);
                            postList.UnionWith(postsInPage);
                            PageScanned(this, new PageScannedEventArgs<Post>(currentPage, (currentPage - topicPage) / (float)pagesToScan, postsInPage));
                            await Task.Delay(webSettings.requestDelay);
                            cancelToken.CancelIfRequestedAndNotDefault();
                            break;
                        }
                        catch (WebException)
                        {
                            ScanFailure(this, new ScannerFailureEventArgs((counter == restartLimit - 1 ? "Last attempt: " : "Attempt #" + (counter + 1) + ":") +
                                "Download of topic page #" + currentPage + " failed. Waiting and attempting to fetch page again."));
                            await Task.Delay(webSettings.requestDelay * counter);
                        }
                    }
                }

                cancelToken.CancelIfRequestedAndNotDefault();
            }
            return postList;
        }

        /// <summary>
        /// Get all posts from the topic
        /// </summary>
        /// <param name="getFullDetails">Should the method collect all information about the posts (may take time but all posts will contain all information about them)?</param>
        /// <param name="cancelToken">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A collection of all the topic's / thread's posts</returns>
        public ICollection<Post> GetAllPosts(bool getFullDetails = false, CancellationToken cancelToken = default(CancellationToken))
        {
            return Task.Run(() => { return GetAllPostsAsync(getFullDetails, cancelToken); }).Result;
        }

        /// <summary>
        /// Get all posts from the topic
        /// </summary>
        /// <param name="getFullDetails">Should the method collect all information about the posts (may take time but all posts will contain all information about them)?</param>
        /// <param name="cancelToken">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A collection of all the topic's / thread's posts</returns>
        public async Task<ICollection<Post>> GetAllPostsAsync(bool getFullDetails = false, CancellationToken cancelToken = default(CancellationToken))
        {
            if (hasAllPosts)
            {
                HashSet<Post> allPosts = new HashSet<Post>();
                foreach (KeyValuePair<int, Post[]> topicPage in postsIndexed)
                {
                    foreach (Post post in topicPage.Value)
                    {
                        allPosts.Add(post);
                    }
                }
                hasAllPosts = true;
                return allPosts;
            }
            else if (canUseAllFunction)
            {

                string html = default(string);

                using (WebClient downloader = new WebClient())
                {
                    if (webSettings.proxyLink != default(string))
                    {
                        downloader.Proxy = new WebProxy(webSettings.proxyLink);
                    }

                    for (int counter = 0; counter < restartLimit; counter++)
                    {
                        try
                        {
                            html = downloader.DownloadString(Link + ";all");
                            await Task.Delay(webSettings.requestDelay);
                            break;
                        }
                        catch (WebException)
                        {
                            cancelToken.CancelIfRequestedAndNotDefault();
                            await Task.Delay(webSettings.requestDelay * counter);
                        }
                    }
                }

                if (html == default(string))
                {
                    throw new BitcointalkConnectionException("Could not retrieve all posts (might not be possible)", typeof(Topic));
                }

                ICollection<Post> allPosts = topicPageParser.ParseTopicPage(html, smileyReplacer);

                //TO DO: NEEDS TESTING
                foreach (Post post in allPosts)
                {
                    int currentPage = (post.NumberInTopic - post.NumberInTopic % 20) / 20 + post.NumberInTopic % 20 == 0 ? 0 : 1;
                    if (!postsIndexed.ContainsKey(currentPage))
                    {
                        postsIndexed.Add(currentPage, new Post[20]);
                    }
                    else
                    {
                        postsIndexed[currentPage][post.NumberInTopic % 20] = post;
                    }
                }
                hasAllPosts = true;
                return allPosts;
            }

            cancelToken.CancelIfRequestedAndNotDefault();

            try
            {
                string html;
                using (WebClient downloader = new WebClient())
                {
                    if (webSettings.proxyLink != default(string))
                    {
                        downloader.Proxy = new WebProxy(webSettings.proxyLink);
                    }

                    html = downloader.DownloadString(@"https://bitcointalk.org/index.php?action=printpage;topic=" + topicID + ".0");
                }
                await Task.Delay(webSettings.requestDelay);
                ICollection<Post> allPosts = topicPageParser.ParseEntireTopic(html, Link);
                return allPosts;
            }
            catch (Exception)
            {
                ScanFailure(this, new ScannerFailureEventArgs("Failed to fetch all posts from topic"));
                throw new BitcointalkConnectionException("Could not retrieve all posts (might not be possible)", typeof(Topic));
            }
        }

        /// <summary>
        /// Perform a deep copy of the Topic object
        /// </summary>
        /// <returns>The selected Topic object's copy</returns>
        public Topic Copy()
        {
            Topic tempTopic = new Topic(Link, new PostParser(webSettings), canUseAllFunction,MaxPages, webSettings);
            foreach (KeyValuePair<int, Post[]> postsInPage in postsIndexed)
            {
                List<Post> tempPostList = new List<Post>();
                foreach (Post post in postsInPage.Value)
                {
                    tempPostList.Add(post.Copy());
                }
                tempTopic.postsIndexed.Add(postsInPage.Key, tempPostList.ToArray());
            }
            return tempTopic;
        }

        public override bool Equals(object obj)
        {
            try
            {
                if (((Topic)obj).Link == this.Link)
                {
                    return true;
                }

            }
            catch (InvalidCastException)
            {
                return false;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Link.GetHashCode();
        }

        /// <summary>
        /// Fetches the number of pages the topic / thread has
        /// </summary>
        public void GetMaxPages()
        {
            string html;
            using (WebClient downloader = new WebClient())
            {
                if (webSettings.proxyLink != default(string))
                {
                    downloader.Proxy = new WebProxy(webSettings.proxyLink);
                }

                html = downloader.DownloadString(Link);
            }
            Task.Delay(webSettings.requestDelay).Wait();
            HtmlDocument parsedHtml = new HtmlDocument();
            parsedHtml.LoadHtml(html);

            int highestPage = 1;
            foreach (HtmlNode node in parsedHtml.DocumentNode.SelectNodes(@"//td[@class=""middletext""]/a[@class=""navPages""]"))
            {
                int tempNumber;
                if (int.TryParse(node.InnerText,out tempNumber))
                {
                    highestPage = tempNumber > highestPage ? tempNumber : highestPage;
                }
            }

            MaxPages = highestPage;

        }

        /// <summary>
        /// Scans a single topic / thread page
        /// </summary>
        /// <param name="currentPage">Page number of the page the posts should be collected from</param>
        /// <returns>A collection of posts from the selected page</returns>
        protected ICollection<Post> ScanTopicPage(int currentPage)
        {
            string topicURL = Link.Remove(Link.Length - topicPostFix.Length+1) + ((currentPage - 1) * 20).ToString();

            string html;
            using (WebClient downloader = new WebClient())
            {
                if (webSettings.proxyLink != default(string))
                {
                    downloader.Proxy = new WebProxy(webSettings.proxyLink);
                }

                html = downloader.DownloadString(topicURL);
            }

            return topicPageParser.ParseTopicPage(html, smileyReplacer);
        }

        /// <summary>
        /// Clear all store posts within the Topic object's cache
        /// </summary>
        protected void ClearCache()
        {
            postsIndexed.Clear();
        }

    }

    /// <summary>
    /// Bitcointalk post
    /// </summary>
    public class Post
    {
        /// <summary>
        /// Settings for fetching web pages within the Post object
        /// </summary>
        public WebConfig webSettings;
        /// <summary>
        /// Text to replace all smiley emoticons in the post with
        /// </summary>
        public string smileyReplacer = ",";
        /// <summary>
        /// The link to the post author's profile
        /// </summary>
        protected string authorLink;
        /// <summary>
        /// The post author's username
        /// </summary>
        protected string authorName;
        /// <summary>
        /// The post's title
        /// </summary>
        protected string title;
        /// <summary>
        /// Post's number within the topic
        /// </summary>
        protected int postNumberInTopic;
        /// <summary>
        /// The time and date the post was created
        /// </summary>
        protected DateTime date;
        /// <summary>
        /// The contents of the post 
        /// (that are stripped of code fields, quotes, images, links (as in URLs), smileys (replaced with the selected character) and new lines / line breaks)
        /// </summary>
        protected string contents;
        /// <summary>
        /// The ID of the post
        /// </summary>
        protected string postId;
        /// <summary>
        /// The ID of the topic the post is in
        /// </summary>
        protected string topicID;
        /// <summary>
        /// Constants used for creating the post's URL
        /// </summary>
        protected const string preTopic = "https://bitcointalk.org/index.php?topic=", postTopic = ".msg", postNumber = "#msg";
        /// <summary>
        /// The object responsible for parsing the HTML of topic pages
        /// </summary>
        protected readonly PostParser postParser;

        /// <summary>
        /// The URL of the post (may be missing if not fetched from topic page using GetMissingDataAsync() or provided upon creation of object)
        /// </summary>
        public string Link
        {
            get
            {
                if ((postId == default(string)) || (topicID == default(string)))
                {
                    return default(string);
                }
                else
                {
                    return preTopic + topicID + postTopic + postId + postNumber + postId;
                }
            }
            protected set
            {
                if (!Regex.IsMatch(value, @"^https://bitcointalk\.org/index\.php\?topic=(\d+)\.msg(\d+)(#msg(\d+))?$"))
                {
                    throw new InvalidBitcointalkInputException("link doesn't match any post pattern", typeof(Post));
                }
                else
                {
                    TopicLinkUnfinished = value;
                    postId = Regex.Replace(Regex.Replace(value, @"^https://bitcointalk\.org/index\.php\?topic=(\d+)\.msg", ""), @"#msg(\d+)$","");
                }
            }
        }
        /// <summary>
        /// The contents of the post 
        /// (that are stripped of [code] fields, quotes, images, links (as in <a href='something.com'></a>), smileys (replaced with a selected character) and new lines / line breaks).
        /// (if not fetched from topic page using GetMissingDataAsync())
        /// </summary>
        public string Message
        {
            get
            {
                return contents;
            }
            protected set
            {
                contents = value;
            }
        }
        /// <summary>
        /// The link to the post author's profile (may be missing if not fetched from topic page using GetMissingDataAsync())
        /// </summary>
        public string AuthorLink
        {
            get
            {
                return authorLink;
            }
            protected set
            {
                authorLink = value;
            }
        }
        /// <summary>
        /// The post author's username (may be missing if not fetched from topic page using GetMissingDataAsync())
        /// </summary>
        public string AuthorName
        {
            get
            {
                return authorName;

            }
            protected set
            {
                authorName = value;
            }
        }
        /// <summary>
        /// The post's title (may be missing if not fetched from topic page using GetMissingDataAsync())
        /// </summary>
        public string Title
        {
            get
            {
                return title;

            }
            protected set
            {
                title = value;
            }
        }
        /// <summary>
        /// Post's number within the topic (may be missing if not fetched from topic page using GetMissingDataAsync() or provided upon creation of object)
        /// </summary>
        public int NumberInTopic
        {
            get
            {
                return postNumberInTopic;

            }
            protected set
            {
                postNumberInTopic = value;
            }
        }
        /// <summary>
        /// The time and date the post was created (may be missing if not fetched from topic page using GetMissingDataAsync())
        /// </summary>
        public DateTime Date
        {
            get
            {
                return date;

            }
            protected set
            {
                date = value;
            }
        }
        /// <summary>
        /// The unfinished URL of the topic the post is in (e.g. https://bitcointalk.org/index.php?topic=1)
        /// </summary>
        protected string TopicLinkUnfinished
        {
            get
            {
                if (topicID != default(string))
                {
                    return preTopic + topicID;
                }
                else
                {
                    return default(string);
                }
            }
            set
            {
                if (!Regex.IsMatch(value, @"topic=(\d+)"))
                {
                    throw new InvalidBitcointalkInputException("link doesn't match topic pattern", typeof(Topic));
                }
                else
                {
                    topicID = Regex.Match(value, @"topic=(\d+)").Value.Replace("topic=", "");
                }
            }
        }


        /// <summary>
        /// Creation of a Post object
        /// </summary>
        /// <param name="postLink">URL of the post</param>
        /// <param name="webSettings">Settings for fetching web pages within the Board object</param>
        /// <param name="postParser">The object responsible for parsing the HTML of topic / thread pages</param>
        public Post(string postLink, WebConfig webSettings, PostParser postParser = default(PostParser))
        {
            Link = postLink;
            if (postParser != default(PostParser))
            {
                this.postParser = postParser;
            }
            else
            {
                postParser = new PostParser(webSettings);
            }
            this.webSettings = webSettings;
        }

        /// <summary>
        /// Creation of a Post object
        /// </summary>
        /// <param name="topicLink">URL of the topic the post is in</param>
        /// <param name="postNumberInTopic">The number of the post within the topic</param>
        /// <param name="webSettings">Settings for fetching web pages within the Board object</param>
        /// <param name="postParser">The object responsible for parsing the HTML of topic / thread pages</param>
        public Post(string topicLink, int postNumberInTopic, WebConfig webSettings, PostParser postParser = default(PostParser))
        {
            TopicLinkUnfinished = topicLink;
            NumberInTopic = postNumberInTopic;
            if (postParser != default(PostParser))
            {
                this.postParser = postParser;
            }
            else
            {
                postParser = new PostParser(webSettings);
            }
            this.webSettings = webSettings;
        }

        /// <summary>
        /// Creation of a Post object
        /// </summary>
        /// <param name="topicLink">URL of the topic the post is in</param>
        /// <param name="authorLink">The link to the post author's profile</param>
        /// <param name="date">The date and time the post was created</param>
        /// <param name="webSettings">Settings for fetching web pages within the Board object</param>
        /// <param name="postParser">The object responsible for parsing the HTML of topic / thread pages</param>
        public Post(string topicLink, string authorLink, DateTime date, WebConfig webSettings, PostParser postParser = default(PostParser))
        {
            TopicLinkUnfinished = topicLink;
            AuthorLink = authorLink;
            Date = date;
            if (postParser != default(PostParser))
            {
                this.postParser = postParser;
            }
            else
            {
                postParser = new PostParser(webSettings);
            }
            this.webSettings = webSettings;
        }

        /// <summary>
        /// Forced creation of a Post object
        /// </summary>
        /// <param name="postLink">URL of the post</param>
        /// <param name="message">The post's message</param>
        /// <param name="authorLink">The link to the post author's profile</param>
        /// <param name="authorName">The username of the post author</param>
        /// <param name="title">The title of the post</param>
        /// <param name="numberInTopic">The number of the post within the topic</param>
        /// <param name="date">The date and time the post was created</param>
        /// <param name="webSettings">Settings for fetching web pages within the Board object</param>
        /// <param name="postParser">The object responsible for parsing the HTML of topic / thread pages</param>
        protected internal Post (string postLink, string message, string authorLink, string authorName, string title, int numberInTopic, DateTime date, WebConfig webSettings, PostParser postParser = default(PostParser))
        {
            Link = postLink;
            Message = message;
            AuthorLink = authorLink;
            AuthorName = AuthorName;
            Title = title;
            NumberInTopic = numberInTopic;
            Date = date;
            if (postParser != default(PostParser))
            {
                this.postParser = postParser;
            }
            else
            {
                postParser = new PostParser(webSettings);
            }
            this.webSettings = webSettings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topicLink">URL of the topic the post is in</param>
        /// <param name="postNumberInTopic">The number of the post within the topic</param>
        /// <param name="message">The post's message</param>
        /// <param name="webSettings">Settings for fetching web pages within the Board object</param>
        /// <param name="postParser">The object responsible for parsing the HTML of topic / thread pages</param>
        protected internal Post(string topicLink, int postNumberInTopic, string message, WebConfig webSettings, PostParser postParser = default(PostParser))
        {
            TopicLinkUnfinished = topicLink;
            NumberInTopic = postNumberInTopic;
            if (postParser != default(PostParser))
            {
                this.postParser = postParser;
            }
            else
            {
                postParser = new PostParser(webSettings);
            }
            Message = message;
            this.webSettings = webSettings;
        }

        /// <summary>
        /// Retrieve missing post data from Bitcointalk
        /// </summary>
        public void GetMissingData()
        {
            Task.Run(GetMissingDataAsync).Wait();
        }


        /// <summary>
        /// Retrieve missing post data from Bitcointalk asynchronously
        /// </summary>
        public async Task GetMissingDataAsync()
        {

            if ((Link != default(string)) && (Message == default(string)))
            {
                string html;
                using (WebClient downloader = new WebClient())
                {
                    if (webSettings.proxyLink != default(string))
                    {
                        downloader.Proxy = new WebProxy(webSettings.proxyLink);
                    }

                    html = downloader.DownloadString(Link);
                }
                await Task.Delay(webSettings.requestDelay);
                IEnumerable<Post> allPosts = postParser.ParseTopicPage(html, smileyReplacer);
                foreach (Post post in allPosts)
                {
                    if (post == this)
                    {
                        Message = post.Message;
                        AuthorLink = post.AuthorLink;
                        AuthorName = post.AuthorName;
                        Title = post.Title;
                        NumberInTopic = post.NumberInTopic;
                        Date = post.Date;
                        break;
                    }
                }
            }
            else if ((Link == default(string)) && (TopicLinkUnfinished != default(string)) && (postNumberInTopic != default(int)))
            {
                string html;
                using (WebClient downloader = new WebClient())
                {
                    if (webSettings.proxyLink != default(string))
                    {
                        downloader.Proxy = new WebProxy(webSettings.proxyLink);
                    }

                    html = downloader.DownloadString(TopicLinkUnfinished + "." + ((postNumberInTopic % 20 == 0) ? (postNumberInTopic - 20) : (postNumberInTopic / 20 * 20)) );
                }
                await Task.Delay(webSettings.requestDelay);
                IEnumerable<Post> allPosts = postParser.ParseTopicPage(html);
                foreach (Post post in allPosts)
                {
                    if (post.NumberInTopic == NumberInTopic)
                    {
                        Link = post.Link;
                        Message = post.Message;
                        AuthorLink = post.AuthorLink;
                        AuthorName = post.AuthorName;
                        Title = post.Title;
                        Date = post.Date;
                        break;
                    }
                }
                if (Link == default(string))
                {
                    throw new InvalidBitcointalkInputException("can't retrieve remote information about post", typeof(Post));
                }
            }
            else if ((Link == default(string)) && (TopicLinkUnfinished != default(string)) && (AuthorLink != default(string)) && (Date != default(DateTime)))
            {
                Topic tempTopic = new Topic(TopicLinkUnfinished + ".0", webSettings,postParser);
                IEnumerable<Post> allPosts = await tempTopic.GetAllPostsAsync(true);
                await Task.Delay(webSettings.requestDelay);
                foreach (Post post in allPosts)
                {
                    if ((post.Date == Date) && (post.AuthorLink == AuthorLink))
                    {
                        Link = post.Link;
                        Message = post.Message;
                        AuthorName = post.AuthorName;
                        Title = post.Title;
                        postNumberInTopic = post.postNumberInTopic;
                        break;
                    }
                }
                if (Link == default(string))
                {
                    throw new InvalidBitcointalkInputException("can't retrieve remote information about post", typeof(Post));
                }
            }
        }

        public override bool Equals(object obj)
        {
            try
            {
                if (((Post)obj).Link != default(string) && Link != default(string) && ((Post)obj).Link == Link)
                {
                    return true;
                }
                else if (
                    ((Post)obj).TopicLinkUnfinished != default(string) && TopicLinkUnfinished != default(string) && ((Post)obj).TopicLinkUnfinished == TopicLinkUnfinished
                    && ((Post)obj).NumberInTopic != default(int) && NumberInTopic != default(int) && ((Post)obj).NumberInTopic == NumberInTopic
                    )
                {
                    return true;
                }
                else if (
                    ((Post)obj).TopicLinkUnfinished != default(string) && TopicLinkUnfinished != default(string) && ((Post)obj).TopicLinkUnfinished == TopicLinkUnfinished
                    && ((Post)obj).Date != default(DateTime) && Date != default(DateTime) && ((Post)obj).Date == Date
                    && ((Post)obj).AuthorLink != default(string) && AuthorLink != default(string) && ((Post)obj).AuthorLink == AuthorLink
                    )
                {
                    return true;
                }
                else if (base.Equals(obj))
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            if (Link != default(string))
            {
                return Link.GetHashCode();
            }
            else if (TopicLinkUnfinished != default(string) && NumberInTopic != default(int))
            {
                return (TopicLinkUnfinished + NumberInTopic).GetHashCode();
            }
            else if ((TopicLinkUnfinished != default(string) && Date != default(DateTime) && AuthorLink != default(string)))
            {
                return (TopicLinkUnfinished + Date + AuthorLink).GetHashCode();
            }
            else 
            {
                return base.GetHashCode();
            }
        }

        /// <summary>
        /// Return a semi-deep (except string; doesn't make a difference) copy of a post.
        /// </summary>
        /// <returns>The copy of the selected Post object</returns>
        public Post Copy()
        {
            Post tempPost = new Post(Link, webSettings,postParser);
            tempPost.Message = Message;
            tempPost.AuthorLink = AuthorLink;
            tempPost.AuthorName = AuthorName;
            tempPost.Title = Title;
            tempPost.NumberInTopic = NumberInTopic;
            tempPost.Date = Date;
            return tempPost;
        }
    }

    /// <summary>
    /// Utility class meant for parsing Bitcointalk pages. Not meant for direct use.
    /// </summary>
    public class PostParser
    {
        /// <summary>
        /// Event, called every time the processing of Bitcointalk data fails
        /// </summary>
        public event EventHandler<ScannerFailureEventArgs> ScannerFailure = delegate { };
        /// <summary>
        /// Settings for fetching web pages within the Board object
        /// </summary>
        public WebConfig config;

        /// <summary>
        /// Create a new PostParser object
        /// </summary>
        /// <param name="config">Settings for fetching web pages within the Board object</param>
        public PostParser(WebConfig config)
        {
            this.config = config;
        }

        /// <summary>
        /// Parse a regular topic / thread page
        /// </summary>
        /// <param name="html">The HTML code of the topic / thread page</param>
        /// <param name="smileyReplacer">Text to replace all the smiley emoticons with</param>
        /// <returns>A collection of posts from the parsed topic</returns>
        public ICollection<Post> ParseTopicPage(string html, string smileyReplacer = ",")
        {

            HashSet<Post> postList = new HashSet<Post>();

            try
            {
                HtmlDocument parsedHtml = new HtmlDocument();


                //////////////////////// Data stripping ////////////////////////
                html = html.Replace("</div>", "</div><br />");
                MatchCollection allSmileys = Regex.Matches(html, @"<img src=""https://bitcointalk\.org/Smileys/default/[a-z\s]+\.gif"" alt=""[a-zA-Z\s]+"" border=""0"" />");
                foreach (Match smileyMatch in allSmileys)
                {
                    html = html.Replace(smileyMatch.Value, smileyReplacer);
                }



                parsedHtml.LoadHtml(html);


                string todayDateString = parsedHtml.DocumentNode.SelectSingleNode(@"//span[@class=""smalltext""]").InnerText;

                HtmlNode tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quoteheader""]");
                while (tempNode != null)
                {
                    tempNode.ParentNode.RemoveChild(tempNode);
                    tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quoteheader""]");
                }

                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quote""]");
                while (tempNode != null)
                {
                    tempNode.ParentNode.RemoveChild(tempNode);
                    tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quote""]");
                }


                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""code""]");
                while (tempNode != null)
                {
                    tempNode.ParentNode.RemoveChild(tempNode);
                    tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""code""]");
                }

                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/br");
                while (tempNode != null)
                {
                    tempNode.ParentNode.RemoveChild(tempNode);
                    tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/br");
                }


                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]//a[@href]");
                while (tempNode != null)
                {
                    tempNode.ParentNode.RemoveChild(tempNode, true);
                    tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]//a[@href]");
                }

                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//img[@class=""userimg""]");
                while (tempNode != null)
                {
                    tempNode.ParentNode.RemoveChild(tempNode);
                    tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//img[@class=""userimg""]");
                }

                //////////////////////// End of data stripping ////////////////////////

                HtmlNodeCollection allPosts = parsedHtml.DocumentNode.SelectNodes(@"//td[@class=""td_headerandpost""]");
                string brokePost = default(string);
                if ((allPosts.Count > 1) && (Regex.IsMatch(allPosts[1].SelectSingleNode(@"div[@class=""post""]").InnerText, @"[0-9]+")))
                {
                    brokePost = allPosts[1].SelectSingleNode(@"div[@class=""post""]").InnerText;
                }


                foreach (HtmlNode postContainer in allPosts)
                {
                    if ((brokePost != default(string)) && (postContainer.SelectSingleNode(@"div[@class=""post""]").InnerText == brokePost))
                    {
                        continue;
                    }

                    string dateString = "";

                    dateString = postContainer.SelectSingleNode(@".//div[@class=""smalltext""]").InnerText;
                    DateTime date;

                    Func<string, int> hourParser = (dateWithHour) =>
                    {
                        int foundHour;
                        if (dateWithHour.Contains("Today"))
                        {
                            foundHour = int.Parse(Regex.Match(dateWithHour, @"Today at \d{2}:").Value.Replace("Today at ", "").Replace(":", ""));
                        }
                        else
                        {
                            foundHour = int.Parse(Regex.Match(dateWithHour, @", \d{2}:").Value.Replace(", ", "").Replace(":", ""));
                        }

                        if (Regex.IsMatch(dateWithHour, "PM$"))
                        {
                            return foundHour;
                        }
                        else
                        {
                            if (foundHour == 12) { return 0; } else { return foundHour; }
                        }
                    };

                    if (Regex.IsMatch(dateString, @"^[A-Z][a-z]+ \d{2}, \d{4}, (\d{2}:){2}\d{2} (AM|PM)$"))
                    {
                        date = new DateTime(
                            int.Parse(Regex.Match(dateString, @"\d{4}").Value),
                            DateTime.ParseExact(Regex.Match(dateString, @"^[A-Z][a-z]+").Value, "MMMM", CultureInfo.InvariantCulture).Month,
                            int.Parse(Regex.Match(dateString, @" \d{2},").Value.Replace(",", "").Replace(" ", "")),
                            hourParser(dateString),
                            int.Parse(Regex.Match(dateString, @":\d{2}:").Value.Replace(":", "")),
                            int.Parse(Regex.Match(dateString, @":\d{2} ").Value.Replace(":", "").Replace(" ", ""))
                            );
                    }
                    else
                    {
                        date = new DateTime(
                            int.Parse(Regex.Match(todayDateString, @"\d{4}").Value),
                            DateTime.ParseExact(Regex.Match(todayDateString, @"^[A-Z][a-z]+").Value, "MMMM", CultureInfo.InvariantCulture).Month,
                            int.Parse(Regex.Match(todayDateString, @" \d{2},").Value.Replace(",", "").Replace(" ", "")),
                            hourParser(dateString),
                            int.Parse(Regex.Match(dateString, @":\d{2}:").Value.Replace(":", "")),
                            int.Parse(Regex.Match(dateString, @":\d{2} ").Value.Replace(":", "").Replace(" ", ""))
                            );
                    }

                    try
                    {
                        postList.Add(new Post(
                            postContainer.SelectSingleNode(@".//a[@class=""message_number""]").GetAttributeValue("href", ""),
                            WebUtility.HtmlDecode(postContainer.SelectSingleNode(@".//div[@class=""post""]").InnerText),
                            postContainer.ParentNode.SelectSingleNode(@".//td[@class=""poster_info""]/b/a[@href]").GetAttributeValue("href", ""),
                            postContainer.ParentNode.SelectSingleNode(@".//td[@class=""poster_info""]/b/a[@href]").InnerText,
                            postContainer.SelectSingleNode(@".//div[@class=""subject""]/a[@href]").InnerText,
                            int.Parse(postContainer.SelectSingleNode(@".//a[@class=""message_number""]").InnerText.Replace("#", "")),
                            date,
                            config,
                            new PostParser(config)
                            ));
                    }
                    catch (Exception)
                    {
                        try
                        {
                            postList.Add(new Post(
                                postContainer.SelectSingleNode(@".//a[@class=""message_number""]").GetAttributeValue("href", ""),
                                int.Parse(postContainer.SelectSingleNode(@".//a[@class=""message_number""]").InnerText.Replace("#", "")),
                                WebUtility.HtmlDecode(postContainer.SelectSingleNode(@".//div[@class=""post""]").InnerText),
                                config,
                                new PostParser(config)
                                ));
                        }
                        catch (Exception)
                        {
                            ScannerFailure(this, new ScannerFailureEventArgs("Failed to fetch post content"));
                        }
                    }
                }
            }
            catch
            {
                ScannerFailure(this, new ScannerFailureEventArgs("Unknown error occured while parsing topic page. Skipping page."));
            }

            return postList;
        }

        /// <summary>
        /// Parse a topic page retrieved via the "print" function
        /// </summary>
        /// <param name="html">The HTML code of the topic's / thread's "print" page</param>
        /// <param name="url">The topic's URL</param>
        /// <returns>A collection of posts from the parsed topic</returns>
        public ICollection<Post> ParseEntireTopic(string html, string url)
        {
            HtmlDocument parsedHtml = new HtmlDocument();

            //////////////////////// Data stripping ////////////////////////
            html = html.Replace("</div>", "</div><br />");


            parsedHtml.LoadHtml(html);

            HtmlNode tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""quoteheader""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""quoteheader""]");
            }

            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""quote""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""quote""]");
            }


            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""code""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""code""]");
            }

            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//a[@href]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode, true);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//a[@href]");
            }

            //////////////////////// End of data stripping ////////////////////////

            int counter = 0;
            List<Post> postList = new List<Post>();
            foreach (HtmlNode postContainer in parsedHtml.DocumentNode.SelectNodes(@"//div[@style=""margin: 0 5ex;""]"))
            {
                counter++;
                try
                {
                    postList.Add(new Post(parsedHtml.DocumentNode.SelectSingleNode(@"//link[@rel=""canonical""]").GetAttributeValue("href", ""),
                        counter,
                        WebUtility.HtmlDecode(postContainer.InnerText),
                        config,
                        new PostParser(config)
                        ));
                }
                catch (Exception)
                {
                    ScannerFailure(this, new ScannerFailureEventArgs("Failed to fetch post content"));
                }
            }


            return postList;
        }

        /// <summary>
        /// Parse the "Recent Posts" page
        /// </summary>
        /// <param name="html">The HTML code of the "Recent Posts" page</param>
        /// <param name="smileyReplacer">Text to replace all the smiley emoticons with</param>
        /// <returns></returns>
        public ICollection<Post> ParseRecentPage(string html, string smileyReplacer = ",")
        {
            HashSet<Post> postList = new HashSet<Post>();
            HtmlDocument parsedHtml = new HtmlDocument();


            //////////////////////// Data stripping ////////////////////////
            html = html.Replace("</div>", "</div><br />");
            MatchCollection allSmileys = Regex.Matches(html, @"<img src=""https://bitcointalk\.org/Smileys/default/[a-z\s]+\.gif"" alt=""[a-zA-Z\s]+"" border=""0"" />");
            foreach (Match smileyMatch in allSmileys)
            {
                html = html.Replace(smileyMatch.Value, smileyReplacer);
            }



            parsedHtml.LoadHtml(html);

            string todayDateString = parsedHtml.DocumentNode.SelectSingleNode(@"//span[@class=""smalltext""]").InnerText;

            HtmlNode tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quoteheader""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quoteheader""]");
            }

            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quote""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""quote""]");
            }


            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""code""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/div[@class=""code""]");
            }

            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/br");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]/br");
            }


            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]//a[@href]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode, true);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//div[@class=""post""]//a[@href]");
            }

            tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//img[@class=""userimg""]");
            while (tempNode != null)
            {
                tempNode.ParentNode.RemoveChild(tempNode);
                tempNode = parsedHtml.DocumentNode.SelectSingleNode(@"//img[@class=""userimg""]");
            }

            //////////////////////// End of data stripping ////////////////////////

            HtmlNodeCollection allPosts = parsedHtml.DocumentNode.SelectNodes(@"//div[@class=""post""]");


            foreach (HtmlNode postContainer in allPosts)
            {

                string dateString = "";

                dateString = postContainer.ParentNode.ParentNode.ParentNode.SelectSingleNode(@".//tr[@class=""titlebg2""]/td[@class=""middletext""]/div[@align=""right""]").InnerText;
                DateTime date;


                Func<string, int> hourParser = (dateWithHour) =>
                {
                    int foundHour;
                    if (dateWithHour.Contains("Today"))
                    {
                        foundHour = int.Parse(Regex.Match(dateWithHour, @"Today at \d{2}:").Value.Replace("Today at ", "").Replace(":", ""));
                    }
                    else
                    {
                        foundHour = int.Parse(Regex.Match(dateWithHour, @", \d{2}:").Value.Replace(", ", "").Replace(":", ""));
                    }

                    if (Regex.IsMatch(dateWithHour, "PM$"))
                    {
                        return foundHour;
                    }
                    else
                    {
                        if (foundHour == 12) { return 0; } else { return foundHour; }
                    }
                };

                if (Regex.IsMatch(dateString, @"^on: [A-Z][a-z]+ \d{2}, \d{4}, (\d{2}:){2}\d{2} (AM|PM)$"))
                {
                    date = new DateTime(
                        int.Parse(Regex.Match(dateString, @"\d{4}").Value),
                        DateTime.ParseExact(Regex.Match(dateString, @"[A-Z][a-z]+").Value, "MMMM", CultureInfo.InvariantCulture).Month,
                        int.Parse(Regex.Match(dateString, @" \d{2},").Value.Replace(",", "").Replace(" ", "")),
                        hourParser(dateString),
                        int.Parse(Regex.Match(dateString, @":\d{2}:").Value.Replace(":", "")),
                        int.Parse(Regex.Match(dateString, @":\d{2} ").Value.Replace(":", "").Replace(" ", ""))
                        );
                }
                else
                {

                    date = new DateTime(
                        int.Parse(Regex.Match(todayDateString, @"\d{4}").Value),
                        DateTime.ParseExact(Regex.Match(todayDateString, @"^[A-Z][a-z]+").Value, "MMMM", CultureInfo.InvariantCulture).Month,
                        int.Parse(Regex.Match(todayDateString, @" \d{2},").Value.Replace(",", "").Replace(" ", "")),
                        hourParser(dateString),
                        int.Parse(Regex.Match(dateString, @":\d{2}:").Value.Replace(":", "")),
                        int.Parse(Regex.Match(dateString, @":\d{2} ").Value.Replace(":", "").Replace(" ", ""))
                        );
                }

                try
                {
                    postList.Add(new Post(
                        postContainer.ParentNode.ParentNode.ParentNode.
                            SelectSingleNode(@".//tr[@class=""titlebg2""]/td[@class=""middletext""]/div[@style=""float: left;""]/b/a[@href]").GetAttributeValue("href", ""),
                        WebUtility.HtmlDecode(postContainer.InnerText),
                        postContainer.ParentNode.ParentNode.ParentNode.
                            SelectNodes(@".//td[@class=""catbg""]/span[@class=""middletext""]/a[@href]")[1].GetAttributeValue("href", ""),
                        postContainer.ParentNode.ParentNode.ParentNode.
                            SelectNodes(@".//td[@class=""catbg""]/span[@class=""middletext""]/a[@href]")[1].InnerText,
                        postContainer.ParentNode.ParentNode.ParentNode.
                            SelectSingleNode(@".//tr[@class=""titlebg2""]/td[@class=""middletext""]/div[@style=""float: left;""]/b/a[@href]").InnerText,
                        default(int),
                        date,
                        config,
                        new PostParser(config)
                        ));
                }
                catch (Exception)
                {
                    ScannerFailure(this, new ScannerFailureEventArgs("Failed to fetch post content"));
                }
            }

            return postList;
        }
    }

    /// <summary>
    /// Class used for fetching posts from the "Recent Posts" page
    /// </summary>
    public class RecentPosts
    {
        /// <summary>
        /// The settings for fetching web pages
        /// </summary>
        public static WebConfig webSettings = new WebConfig(2000);
        /// <summary>
        /// Event, called every time a Bitcointalk page with posts is processed
        /// </summary>
        public static event EventHandler<PageScannedEventArgs<Post>> PageScanned = delegate { };
        /// <summary>
        /// Event, called every time the processing of a Bitcointalk page with posts fails
        /// </summary>
        public static event EventHandler<ScannerFailureEventArgs> ScanFailure = delegate { };
        /// <summary>
        /// The number of times the RecentPosts class methods should try to retrieve a Bitcointalk page
        /// </summary>
        public const int restartLimit = 4;


        /// <summary>
        /// Retrieve posts from a selected number of "Recent Posts" page
        /// </summary>
        /// <param name="pages">Number of pages to collect posts from</param>
        /// <param name="cancelToken">Cancellation token, used for ceasing the continued execution of the method</param>
        /// <returns>A Task which, once completed, returns a collection of posts from the selected number of "Recent Posts" pages</returns>
        public static async Task<ICollection<Post>> GetAsync(int pages, CancellationToken cancelToken = default(CancellationToken))
        {
            if (pages < 1)
            {
                return null;
            }

            if (pages > 10)
            {
                pages = 10;
            }


            string html= default(string);
            using (WebClient downloader = new WebClient())
            {
                if (webSettings.proxyLink != default(string))
                {
                    downloader.Proxy = new WebProxy(webSettings.proxyLink);
                }

                for (int pageNum =0; pageNum < pages; pageNum++)
                {
                    for (int counter = 0; counter < restartLimit; counter++)
                    {
                        try
                        {
                            html = WebUtility.HtmlDecode(downloader.DownloadString(@"https://bitcointalk.org/index.php?action=recent;start=" + pageNum*10));
                            await Task.Delay(webSettings.requestDelay);
                            break;
                        }
                        catch (WebException)
                        {
                            cancelToken.CancelIfRequestedAndNotDefault();
                            await Task.Delay(webSettings.requestDelay * counter);
                        }
                    }
                }

            }
            if (html == default(string))
            {
                throw new BitcointalkConnectionException("Exceeded connection attempts",typeof(RecentPosts));
            }

            PostParser parser = new PostParser(webSettings);
            parser.ScannerFailure += delegate (object obj, ScannerFailureEventArgs e) { ScanFailure(obj, new ScannerFailureEventArgs(e.FailureMessage)); };
            ICollection<Post> pagePosts = parser.ParseRecentPage(html);

            return pagePosts;
        }


    }

}
