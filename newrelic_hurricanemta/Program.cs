using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;
using ConfigPS;

namespace newrelic_hurricanemta
{
    class Program
    {
        static void Main(string[] args)
        {
            dynamic global = new ConfigPS.Global();

            //http://www.codeproject.com/Articles/7568/Tail-NET

            HostFactory.Run(x =>
            {
                x.Service<ThreadManager>(s =>
                {
                    s.ConstructUsing(name => new ThreadManager((string)global.MtaAccountRoot, (int[])global.MtaAccountList));
                    s.WhenStarted(lt => lt.Start());
                    s.WhenStopped(lt => lt.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("New Relic Hurricane MTA Agent");
                x.SetDisplayName("newrelic_hurricanemta");
                x.SetServiceName("newrelic_hurricanemta");
            });
        }
    }
}
