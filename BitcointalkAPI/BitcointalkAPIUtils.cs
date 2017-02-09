using System;
using System.Collections.Generic;
using System.Threading;

namespace BitcointalkAPI
{
    namespace Utilities
    {

        /// <summary>
        /// Event arguments for a successful parsing of a Bitcontalk page
        /// </summary>
        /// <typeparam name="T">The type of elements the Bitcointalk page contains</typeparam>
        public class PageScannedEventArgs<T> : EventArgs
        {
            /// <summary>
            /// The progress of the overall processing of multiple pages
            /// </summary>
            private float progress;

            /// <summary>
            /// The page number of the Bitcointalk page that was parsed
            /// </summary>
            public int PageScanned { get; protected set; }
            /// <summary>
            /// The progress of the overall processing of multiple pages
            /// </summary>
            public float Progress
            {
                get
                {
                    return progress;
                }
                protected set
                {
                    if (value > 100)
                    {
                        progress = 100;
                    }
                    else if (value < 0)
                    {
                        progress = 0;
                    }
                    else
                    {
                        progress = value;
                    }
                }
            }
            /// <summary>
            /// The collection of elements within the parsed page
            /// </summary>
            public ICollection<T> ElementsInPage { get; protected set; }

            /// <summary>
            /// Create a new PageScannedEvent args object
            /// </summary>
            /// <param name="pageScanned">The page number of the Bitcointalk page that was parsed</param>
            /// <param name="progress">The progress of the overall processing of multiple pages</param>
            /// <param name="elementsInPage">The collection of elements within the parsed page</param>
            public PageScannedEventArgs(int pageScanned, float progress, ICollection<T> elementsInPage = null)
            {
                PageScanned = pageScanned;
                Progress = progress;
                ElementsInPage = elementsInPage;
            }
        }

        /// <summary>
        /// Event arguments for a failure while parsing Bitcointalk pages
        /// </summary>
        public class ScannerFailureEventArgs : EventArgs
        {
            /// <summary>
            /// Information about the failure
            /// </summary>
            public string FailureMessage;

            /// <summary>
            /// Create a new ScannerFailureEventArgs object
            /// </summary>
            /// <param name="failureMessage">Information about the failure</param>
            public ScannerFailureEventArgs(string failureMessage)
            {
                FailureMessage = failureMessage;
            }
        }

        /// <summary>
        /// Exception class for invalid input provided to a BitcointalkAPI class
        /// </summary>
        public class InvalidBitcointalkInputException : Exception
        {
            /// <summary>
            /// Create a new InvalidBitcointalkInputException exception with a default message
            /// </summary>
            public InvalidBitcointalkInputException() : base("Invalid input for Bitcointalk API class.")
            {
            }

            /// <summary>
            /// Create a new InvalidBitcointalkInputException exception
            /// </summary>
            /// <param name="message">Information about the incorrect input</param>
            /// <param name="type">The type of object the input was provided for</param>
            public InvalidBitcointalkInputException(string message, Type type) : base("Invalid input for " + type.ToString().ToLower() + ":" + message + ".")
            {
            }
        }

        /// <summary>
        /// Exception class for the inability to establish a connection to the Bitcointalk's servers
        /// </summary>
        public class BitcointalkConnectionException : Exception
        {
            /// <summary>
            /// Create a new BitcointalkConnectionException exception with a default message
            /// </summary>
            public BitcointalkConnectionException() : base("Can't connect to Bitcoinalk to fetch data")
            {
            }

            /// <summary>
            /// Create a new BitcointalkConnectionException exception
            /// </summary>
            /// <param name="message">Information about the failure to connect to Bitcointalk/param>
            /// <param name="type">The type of object that tried to connect to Bitcointalk</param>
            public BitcointalkConnectionException(string message, Type type) : base("Can't connect to Bitcoinalk to fetch data for " + type.ToString().ToLower() + ":" + message + ".")
            {
            }
        }

        /// <summary>
        /// Basic configuration struct for adding delays between connection requests to Bitcointalk and basic proxy functionality
        /// </summary>
        public struct WebConfig
        {
            /// <summary>
            /// Minimum number of miliseconds between connection request to Bitcointalk
            /// </summary>
            public int requestDelay;
            /// <summary>
            /// Proxy URL that should be connected through
            /// </summary>
            public string proxyLink;

            /// <summary>
            /// Create a new WebConfig object
            /// </summary>
            /// <param name="requestDelay">Minimum number of miliseconds between connection request to Bitcointalk</param>
            /// <param name="proxyLink">Proxy URL that should be connected through</param>
            public WebConfig(int requestDelay, string proxyLink = default(string))
            {
                this.requestDelay = requestDelay;
                this.proxyLink = proxyLink;
            }

        }

        /// <summary>
        /// Ease-of-use extension for invoking exceptions on cancellation
        /// </summary>
        public static class CancellationTokenExtension
        {
            /// <summary>
            /// Invoke cancellation exception if it was requested and the token isn't empty
            /// </summary>
            /// <param name="token">Cancellation token</param>
            public static void CancelIfRequestedAndNotDefault(this CancellationToken token)
            {
                if (token != default(CancellationToken))
                {
                    token.ThrowIfCancellationRequested();
                }
            }
        }
    }
    

}
