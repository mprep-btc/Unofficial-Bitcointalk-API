# Unofficial Bitcointalk API
C# / .NET library for fetching Bitcointalk.org boards, topics and posts.

# Features
* Fetch topics from board pages and posts from topic pages or the "Recent Posts" page.
* Get post's message\*, URL, title, number in topic, creation date, author's username and profile link.
* Quickly fetch post messages from the entire topic (other data will require additional fetching).
* Fully commented code for developing extensions / different versions.

<sub>\* The message is stripped of [code] fields, quotes, images, links (as in <a href></a>), smileys (replaced with ',' character) and new lines / line breaks. Feel free to create your own modification.</sub>
## Usage examples

#### Print out all posts from a single topic page
``` cs
/*
using BitcointalkAPI;
using BitcointalkAPI.Utilities;
using System;
using System.Collections.Generic;

...
*/

Topic newTopic = new Topic(@"https://bitcointalk.org/index.php?topic=1.0", new WebConfig(2000));
ICollection<Post> posts = newTopic.GetPosts(1);

foreach (Post post in posts)
{
    Console.WriteLine(post.Message);
}
```

#### Print out all topic links from the first 5 board pages (fetching board pages with a 2 second delay)
``` cs
/*
using BitcointalkAPI;
using BitcointalkAPI.Utilities;
using System;
using System.Collections.Generic;

...
*/

Board board = new Board(@"https://bitcointalk.org/index.php?board=1.0", new WebConfig(2000));
ICollection<Topic> allTopics = board.GetTopics(1, 5);

foreach (Topic topic in allTopics)
{
    Console.WriteLine(topic.Link);
}
```

## Installation instructions

1. Download the latest version of the DLLs from the [Binaries folder](/Binaries);
2. Open up the project you want to use the library for in Microsoft Visual Studio;
3. Add the libraries to your project's "**References**":
  * In Visual Studio Community 2015:
    * Open up the "**Solution Explorer**";
    * Go to "**Solution '*[Your solution name]*'**" > "***[Project name]***";
    * Right click on "**References**" and click "**Add references...**";
    * Select the "**Browse**" tab and click the "**Browse...**" button;
    * Navigate to the "**BitcointalkAPI.dll**" file and select it;
    * Click "**OK**".
4. *(Optional) Include the "using BitcointalkAPI;" and "using BitcointalkAPI.Utilities;" lines in every source file you use the library in for easier access.*

**OR**

1. Clone or download the project's repository;
2. Open up the "**BitcointalkAPI.sln**" project file with Microsoft Visual Studio Community 2015 (or any other compatible Visual Studio version);
3. Go to "**Project**" > "**Manage NuGet Packages...**";
4. Redownload and reinstall the missing "**HTML Agility Pack**" package (Visual Studio should prompt you to do so);
5. Make sure the "**Release**" configuration is selected (at the top; near the "Start button");
6. Build the solution ("**Build**" > "**Build Solution**");
7. Follow the previous instructions from step 2.

## Dependancies

* [HTML Agility Pack](http://www.nuget.org/packages/HtmlAgilityPack) (license: [MS-PL](https://msdn.microsoft.com/en-us/library/ff647676.aspx));
* .NET Framework 4.6.1 (may work with an older version, though untested).

## License
The project's files (except for the ones belonging to the HTML Agility Pack (HTMLAgilityPack.dll and HTMLAgilityPack.xml) in the Binaries folder and subfolders) are licensed under the [AGPL 3.0 license](https://github.com/mprep-btc/Bitcointalk-Post-Iconizer/blob/master/LICENSE).

## Major changes

v0.5 - initial release.

## Donations

Donations are welcome: 1**mprep**xqZeK7LcRYEz84DVJKCvF8CQ8gu
