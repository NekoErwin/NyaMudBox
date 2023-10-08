using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NyaMudBox
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        System.Threading.Mutex? mutex; // 监控是否重复启动，不要 GC 它

        public App()
        {
            this.Startup += new StartupEventHandler(App_Startup);
        }

        void App_Startup(object sender, StartupEventArgs e)
        {
            bool ret;
            mutex = new System.Threading.Mutex(true, "MudBox", out ret);

            if (!ret)
            {
                MessageBox.Show("Another Instance is Running.\n另一个本程序的实例正在运行.");
                Environment.Exit(0);
            }

        }
    }
}
