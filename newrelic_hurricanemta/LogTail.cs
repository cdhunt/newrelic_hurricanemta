using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using RestSharp;
using ConfigPS;
using Base36Encoder;

namespace newrelic_hurricanemta
{
    class ThreadManager
    {
        public List<string> accountPaths { get; set; }
        public List<Thread> ThreadList { get; set; }

        public ThreadManager(string root, int[] accounts)
        {
            accountPaths = new List<string>();

            for (int i=0; i <= accounts.GetUpperBound(0); i++)
            {
                accountPaths.Add(Path.Combine(Path.Combine(root, accounts[i].ToString()), "logfiles") );
            }           
        }

        public void Start()
        {
            ThreadList = new List<Thread>();

            foreach (string path in accountPaths)
            {                
                LogTail lt = new LogTail(path);

                ThreadList.Add(new Thread(new ThreadStart(lt.ThreadRun)));
            }
            
            foreach (Thread t in ThreadList)
            {
                try
                {
                    t.Start();
                }
                catch (ThreadStateException e)
                {
                    Console.WriteLine(e);  // Display text of exception
                }
                catch (ThreadInterruptedException e)
                {
                    Console.WriteLine(e);  // This exception means that the thread
                    // was interrupted during a Wait
                }
            }            
        }

        public void Stop()
        {
            foreach (Thread t in ThreadList)
            {
                if (t.IsAlive)
                    t.Abort();
            }
        }
    }

    public class LogTail
    {
        public string LogPathRoot { get; set; }

        RestClient _client;
        string _nrAccountId;
        string _nrApiKey;

        public LogTail(string path)
        {
            LogPathRoot = path;

            _client = new RestClient("http://insights.newrelic.com/beta_api");
            _client.Proxy = new System.Net.WebProxy("http://127.0.0.1:8888"); //Fiddler

            dynamic global = new ConfigPS.Global();
            _nrAccountId = global.NewRelicAccountID;
            _nrApiKey = global.NewRelicApiKey;
        }

        public MessageEvent SplitLine(string line)
        {
            string[] tokens = line.Split(' ');
            double timestamp = 0;

            if (tokens.GetUpperBound(0) < 13)
            {
                return new MessageEvent { timestamp = timestamp };
            }

            try
            {
                DateTime datetime = DateTime.Parse(string.Format("{0} {1}", tokens[0], tokens[1]));
                timestamp = (datetime - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds;
            }
            catch (Exception)
            {
                Console.WriteLine(string.Format("Failed DateTime.Parse: {0} {1}", tokens[0], tokens[1]));
            }
            string instance = string.Empty;
            string groupid = string.Empty;
            string projectid = string.Empty;
            string failurecode = "none";

            if (!tokens[5].Equals("-"))
            {
                string[] mailingTokens = tokens[5].Split('-');
                instance = mailingTokens[0];
                groupid = mailingTokens[1];
                projectid = mailingTokens[2];
            }

            if (!tokens[13].Equals("-"))
            {
                failurecode = tokens[13];
            }

            string domain = tokens[6].Split('@')[1];

            return new MessageEvent{
                timestamp = timestamp,
                eventType = "MessageDelivery",
                outcome = tokens[2],
                instance = instance,
                groupId = Base36.Decode(groupid).ToString(),
                projectId = Base36.Decode(projectid).ToString(),
                domain = domain,
                failureCode = failurecode,
                size = Int32.Parse(tokens[8]),
            };
        }

        public void SendEvent(MessageEvent e)
        {
            RestRequest request = new RestRequest();
            request.RequestFormat = DataFormat.Json;
            request.Resource = "accounts/{accountId}/events";
            request.AddParameter("accountId", _nrAccountId, ParameterType.UrlSegment)
                .AddHeader("X-Insert-Key", _nrApiKey)
                .AddBody(e);
            request.Method = Method.POST;

            IRestResponse r = _client.Execute(request);
            
            //if (r.StatusCode != System.Net.HttpStatusCode.OK)
            //{
                Console.WriteLine(r.Content);
            //}
        }

        public void ThreadRun()
        {
            string logFilePath = Path.Combine(LogPathRoot, String.Format("{0}Processed.log", DateTime.Now.ToString("yyyyMMdd")));

            Console.WriteLine(string.Format("Tailing: {0}", logFilePath));

            using (StreamReader reader = new StreamReader(new FileStream(logFilePath,
                     FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                //start at the end of the file
                long lastMaxOffset = reader.BaseStream.Length;

                while (true)
                {
                    System.Threading.Thread.Sleep(100);

                    //if the file size has not changed, idle
                    if (reader.BaseStream.Length == lastMaxOffset)
                        continue;

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line = "";
                    while ((line = reader.ReadLine()) != null)
                        SendEvent(SplitLine(line));

                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;
                }
            }
        }
    }
}
