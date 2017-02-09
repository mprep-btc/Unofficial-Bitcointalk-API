# Bitcointalk-API
C# / .NET library for fetching Bitcointalk.org boards, topics and posts

## Installation instructions

1. Download the latest version of the DLLs from the [Binaries folder](/Binaries);
2. Open up the project you want to use the library for in Microsoft Visual Studio;
3. Add the libraries to your project's "**References**":
  * In Visual Studio Community 2015:
    * Open up the "**Solution Explorer**";
    * Go to "**Solution '*[Your solution name]*'**" > "***[Project name]***";
    * Right click on "References" and click "Add references...";
    * Select the "Browse" tab and click the "Browse..." button;
    * Navigate to the "**BitcointalkAPI.dll**" file and select it;
    * Click "**OK**".
4. *(Optional) Include the "using BitcointalkAPI;" line in every source file you use the library in for easier access.*

**OR**

1. Clone or download the project's repository;
2. Open up the "**BitcointalkAPI.sln**" project file with Microsoft Visual Studio Community 2015 (or any other compatible Visual Studio version);
3. Go to "**Project**" > "**Manage NuGet Packages...**";
4. Redownload and reinstall the missing "**HTML Agility Pack**" package (Visual Studio should prompt you to do so);
5. Make sure the "**Release**" configuration is selected (at the top; near the "Start button");
6. Build the solution ("**Build**" > "**Build Solution**");
7. Follow the previous instructions from step 2.


## Usage examples

#### Print out all posts from a single topic page
``` c#
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
``` c#
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

## License
The project's files (except for the ones belonging to the HTML Agility Pack in the Binaries folder) are licensed unde the [AGPL 3.0 license](https://github.com/mprep-btc/Bitcointalk-Post-Iconizer/blob/master/LICENSE).

## Major changes

v0.5 - initial release.

## Donations

Donations are welcome: 1**mprep**xqZeK7LcRYEz84DVJKCvF8CQ8gu
