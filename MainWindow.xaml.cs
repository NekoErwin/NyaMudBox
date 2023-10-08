using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using NyaLang;
using System.Linq.Expressions;
using Expression = System.Linq.Expressions.Expression;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security;
using System.Windows.Markup;
using System.Windows.Interop;

namespace NyaMudBox
{ 
    public partial class MainWindow : Window
    {
        /* UI 任务异常 */
        class MainWindowUILogicError : ApplicationException
        {
            public MainWindowUILogicError(string msg) : base(msg) { }
            public MainWindowUILogicError() : base() { }
        }
        /* 显示警告 */
        void mainWindowWarningLog(string msg)
            => Console.WriteLine("[ MudBox : MainWindow ]: " + msg);

        #region 控制台方法

        /// <summary>
        /// 控制台帮助类
        /// </summary>
        private static class ConsoleHelper
        {
            #region 外部方法
            /// <summary>
            /// 创建控制台
            /// </summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            private static extern bool AllocConsole();
            /// <summary>
            /// 销毁控制台
            /// </summary>
            [DllImport("Kernel32")]
            private static extern void FreeConsole();
            /// <summary>
            /// 获取窗口句柄
            /// </summary>
            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
            /// <summary>
            /// 设置窗体的显示与隐藏
            /// </summary>
            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
            #endregion

            // 控制台句柄
            static IntPtr _consoleHdl = IntPtr.Zero;

            /// <summary>
            /// 显示控制台
            /// </summary>
            public static void showConsole()
            {
                if (_consoleHdl == IntPtr.Zero) // 如果未创建控制台，创建一个
                {
                    AllocConsole();
                    _consoleHdl = FindWindow("ConsoleWindowClass", Console.Title); // 获取句柄
                }
                else // 如果已创建，显示它
                {
                    ShowWindow(_consoleHdl, 1);
                }
            }

            /// <summary>
            /// 隐藏控制台
            /// </summary>
            public static void hideConsole()
            {
                if (_consoleHdl == IntPtr.Zero)
                {
                    MessageBox.Show("无法找到控制台.");
                }
                else // 如果已创建，隐藏它
                {
                    ShowWindow(_consoleHdl, 0);
                }
            }
        }
        #endregion

        #region GameView 输入输出
        /*  在富文本区内自动生成超链接按钮：
         *      在生成时，需要给这个按钮赋一个编号，
         *      当该按钮被按下，就会将 TextLinkClickReturn 的值设置为该编号。
         *      textLinkClickedFlg 用于判断是否有按钮被按下，
         *      每当 TextLinkClickReturn 被赋值，它被置为 true，
         *      每当 TextLinkClickReturn 被读取，它被置为 false
         */
        public int TextLinkClickReturn = -1;
        //private bool textLinkClickedFlg = false; // 按钮是否被按下
        // 循环等待太愚蠢了，cpu性能堪忧，所以改用信号量
        private Semaphore textLinkClickedSema = new(0, 1); // 按钮是否被按下
        private void setTextLinkClickReturn(int v)
        {
            // 如果信号量已满，继续release会报错，因此要先做信号量检测
            if (textLinkClickedSema.WaitOne(0)) // WaitOne(0)被特殊优化：它不会阻塞线程，并立即返回信号量是否为1        
            { // 原本信号量为1：
                mainWindowWarningLog($"按钮返回了数值 '{v}'，未处理的数值 '{TextLinkClickReturn}' 已被覆盖.");
            }
            textLinkClickedSema.Release(); // 信号量 + 1

            TextLinkClickReturn = v;
        }
        /// <summary>
        /// 这个方法只是单纯的返回TextLinkClickReturn的值
        /// 由于TextLinkClickReturn为异步产生，所以获取该值前应当手动进行 textLinkClickedSema.WaitOne()；
        /// 是否阻塞线程、是否设置超时应当由调用本函数的场景决定
        /// </summary>
        private int getTextLinkClickReturn()
        {
            return TextLinkClickReturn;
        }
        /*  textLinkClickActionGenerator 是用于生成 '超链接按钮返回方法' 的工厂函数 */
        private RoutedEventHandler textLinkClickActionGenerator(int textLinkId)
            => (s, e) => { setTextLinkClickReturn(textLinkId); };
        /*  textLinkClickActionList 用于在窗体初始化时储存生成的64个方法函数，避免重复生成委托对象 */
        private RoutedEventHandler[] textLinkClickActionList = new RoutedEventHandler[256];
        /*  gameViewScroller 控制 GameView 文本滚动 */
        ScrollViewer? gameViewScroller;

        /// <summary>
        /// 生成一个超链接按钮，该按钮按下后会将 TextLinkClickReturn 设置为 id 值
        /// </summary>
        /// <returns>返回生成的按钮</returns>
        private Hyperlink generateTextLink(string text, int id)
        {
            Hyperlink hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(text);
            hyperlink.Click += textLinkClickActionList[id];
            return hyperlink;
        }
        /// <summary>
        /// 生成一个超链接按钮，该按钮按下后会将 TextLinkClickReturn 设置为 id 值
        /// </summary>
        /// <returns>返回生成的按钮</returns>
        private Hyperlink generateTextLink(UIElement uIElement, int id)
        {
            Hyperlink hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(uIElement);
            hyperlink.Click += textLinkClickActionList[id];
            return hyperlink;
        }

        /// <summary>
        /// 使所有当前富文本区的所有按钮 disable
        /// </summary>
        private void disableAllTextLink()
        {
            foreach (var a in GameView.Document.Blocks)
            {
                a.IsEnabled = false;
            }
        }
        /// <summary>
        /// 开启一个线程，异步等待 TextLink 按钮按下
        /// </summary>
        /// <param name="disableAllLinkAfterReturn">是否在得到返回后 disable 所有 TextLink</param>
        /// <returns>当按钮按下时，返回按下按钮的编号</returns>
        async Task<int> waitForTextLinkReturn(bool disableAllLinkAfterReturn = true)
        {
            // 异步等待避免线程阻塞
            //      需要加上nyaScriptCanceller，否则脚本主线程推出时这个线程不退出，后面启动的脚本会出现双击现象
            await Task.Run(() => { textLinkClickedSema.WaitOne(); }, nyaScriptCanceller.Token);
            if (disableAllLinkAfterReturn) disableAllTextLink();
            return getTextLinkClickReturn();
        }

        /* 将各种组件推送到 GameView -------------------------------- */
        #region GameView 输出
        private void pushParagraph(Paragraph p)
        {
            GameView.Document.Blocks.Add(p);
            // 控制文本滚动到最下方
            gameViewScroller?.ScrollToBottom();
        }
        private void pushLine(string msg)
        {
            Paragraph p = new();
            p.Inlines.Add(msg);
            pushParagraph(p);
        }
        private void clearView()
            => GameView.Document.Blocks.Clear(); // 清空文本内容

        #endregion

        #endregion

        #region 格式化输出

        public void PushFmtStrLn(string f_str)
        {
            /* 格式:
             *     this is a line\@<button,1>\nanotherline
             * =>
             *     this is a linebutton
             *     anotherline
             *     (其中button按下返回1)
             *     
             *     \@<text,n>   将字符串作为按钮，按下时返回 n，n 取 [0, 255]
             *     @            显示一个 @
             */
            int start = 0; int current = 0;
            int defaultReturnAssign = 0;
            Paragraph p = new();

            while(current < f_str.Length)
            {
                if (f_str[current] == '@' && current > 0 && f_str[current - 1] == '\\')
                {
                    // 按钮声明的情况
                    if (start != current)
                        p.Inlines.Add(f_str.Substring(start, current - start - 1)); // 将字符串部分添加进去
                    current++;
                    // 标签声明缺失'<'
                    // 并不是严重的错误，我们只把它打到屏幕上
                    if (current >= f_str.Length || f_str[current] != '<')
                    {
                        pushParagraph(p);
                        pushLine("FormatError: Expect '<' after button declare symbol '\\@'");
                        return;
                    }
                    current++;
                    start = current;
                    // 检查按钮文本
                    string buttonText;
                    while (current < f_str.Length)
                    {
                        if (f_str[current] == ',')
                        {
                            buttonText = f_str.Substring(start, current - start);

                            current++;
                            start = current;
                            while (current < f_str.Length && f_str[current] != '>')
                                current++;
                            p.Inlines.Add(generateTextLink(
                                buttonText,
                                Convert.ToInt32(f_str.Substring(start, current - start))));

                            current++;
                            start = current;
                            break;
                        }
                        else if (f_str[current] == '>')
                        {
                            buttonText = f_str.Substring(start, current - start);
                            if (defaultReturnAssign >= 256) // 按钮太多
                            {
                                pushParagraph(p);
                                pushLine("FormatError: Too many default buttons ( > 256 ).");
                                return;
                            }
                            p.Inlines.Add(generateTextLink(buttonText, defaultReturnAssign));
                            defaultReturnAssign++;

                            current++;
                            start = current;
                            break;
                        }
                        current++;
                    }
                }
                else
                {
                    current++;
                }
            }

            p.Inlines.Add(f_str.Substring(start, current - start));

            pushParagraph(p);
        }
        #endregion

        #region NyaLang 环境

        Task<int>? nyaScriptTask; // 脚本必须运行在子线程中，这是储存线程的容器
        CancellationTokenSource nyaScriptCanceller = new(); // 线程终止器

        // 通过 Dispatcher 使子线程能够访问 UI 控件的中间方法
        private int _waitInput()
        {
            // 因为本身就在异步线程里，在这里WaitOne环不会阻塞 UI
            textLinkClickedSema.WaitOne();

            nyaScriptCanceller.Token.ThrowIfCancellationRequested(); // 如果收到终止token，抛出异常来终止线程
            // 尝试安全退出写的我人都要麻了
            // CancellationToken 不能真的终止线程，只是发送一个信号量，让线程内部自己处理终止
            // 把这个异常上抛就能中断所以catch以前的处理
            // 在 nyaScriptTask 中catch它，即可终止线程

            // P.S. 在发送 CancellerToken 之后，
            // 需要发送一个 textLinkClickedSema 信号才能退出
            return getTextLinkClickReturn();
        }
        private void _disableAllTextLink()
            => Dispatcher.BeginInvoke((Action)delegate () { disableAllTextLink(); });
        private void _pushLine(string s)
            => Dispatcher.BeginInvoke((Action)delegate () { pushLine(s); });
        private void _pushFmtStrLn(string s)
            => Dispatcher.BeginInvoke((Action)delegate () { PushFmtStrLn(s); });
        private void _clearView()
            => Dispatcher.BeginInvoke((Action)delegate () { clearView(); });

        /// <summary>
        /// 初始化 NyaLang.Runtime.InteractRedirectInterface 中的方法
        /// </summary>
        private void nyaLangInit()
        {
            NyaLang.Runtime.InteractRedirectInterface.PushFormatLineMethod = _pushFmtStrLn;// PushFmtStrLn;
            NyaLang.Runtime.InteractRedirectInterface.PushLineMethod = _pushLine;//pushLine;
            NyaLang.Runtime.InteractRedirectInterface.WaitInputMethod = _waitInput;
            NyaLang.Runtime.InteractRedirectInterface.DisableAllTextLinkMethod = _disableAllTextLink;
            NyaLang.Runtime.InteractRedirectInterface.ClearViewMethod = _clearView;
        }

        #endregion


        #region Game 流程控制

        /// <summary>
        /// GameView 初始化，在第一次操作 GameView 前，应当执行该语句
        /// </summary>
        /// <exception cref="MainWindowUILogicError"></exception>
        private void gameInit()
        {
            TextLinkClickReturn = -1;
            // 生成所有超链接按钮方法
            for (int i = 0; i < 256; i++)
                textLinkClickActionList[i] = textLinkClickActionGenerator(i);

            //使日志富文本区域滚动可控制，注意该语句不能写在窗体初始化里，否则返回 null
            gameViewScroller =
                GameView.Template.FindName("PART_ContentHost", GameView) as ScrollViewer ??
                throw new MainWindowUILogicError("请不要在窗体初始化中获取 PART_ContentHost.");
        }

        /// <summary>
        /// 编译指定脚本，储存到 nyaScriptTask 中
        /// </summary>
        /// <remarks>
        /// 该函数会创建一个子进程，储存在 nyaScriptTask 中；
        /// 如果需要开始运行脚本，使用 nyaScriptTask.Start()；
        /// 如果需要中断运行中的脚本，使用 nyaScriptCanceller.Cancel()
        /// </remarks>
        private void loadScript(string path)
        {
            // 编译文件，参考 NyaLang 项目内容 --------------------------------------------
            Console.WriteLine("[ ScriptLoader ] : Loading...");

            string source = File.ReadAllText(path);

            NyaLang.Core.Scanner scanner = new(source);
            Console.WriteLine("[ ScriptLoader ] : Scanning...");
            var scan_result = scanner.Execute();
            NyaLang.Core.Parser parser = new(scan_result);
            Console.WriteLine("[ ScriptLoader ] : Parsing...");
            Dictionary<string, ParameterExpression?> gloableVal;
            Func<int>? ScriptLam = null;
            try
            {
                var parse_result = parser.Execute(out gloableVal);
                parse_result.Add(Expression.Constant(0)); // 经典 return 0 （编译到lambda必须指定一个返回值类型）
                                                          // 将全局变量命名空间传进去
                var codeBlock = Expression.Block(
                    (from rec in gloableVal.Values.ToList() where rec != null select rec).ToList(),
                    parse_result);
                var lbd = Expression.Lambda<Func<int>>(codeBlock);
                Console.WriteLine("[ ScriptLoader ] : Compiling...");
                ScriptLam = lbd.Compile();

                Console.WriteLine("[ ScriptLoader ] : Done.");
            }
            catch (NyaLang.Core.Parser.ParseError pe)
            {
                pe.Report();
                Console.WriteLine("[ ScriptLoader ] : Canceled.");
            }
            if (ScriptLam == null)
            {
                Console.WriteLine("[ ScriptLoader ] : Compile failed. Exit.");
                return;
            }
            // 编译完成 --------------------------------------------------------------------


            // 创建一个子进程，把脚本生成的方法包装进去
            //     nyaScriptCanceller 允许我们在 ui 线程上终止任务
            nyaScriptTask = new Task<int>(
                () => {
                    int res = 1;
                    try { res = ScriptLam(); }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("[ ScriptTask ] : Thread Cancelled.");
                    }
                    return res;
                }, nyaScriptCanceller.Token);
            
        }

        /// <summary>
        /// 测试功能使用的脚本
        /// </summary>
        private async void __gameTestProgarm()
        {
            Paragraph p;
            int linkReturn;
            
            pushLine(">> Note: 这是一个 GameView 组件的测试程序.");
            pushLine(">> 该程序将示范 GameView 各项功能的使用.");
            for(int i = 0; i < 8; i++)
                pushLine("");
            p = new();
            p.Inlines.Add("                          ");
            p.Inlines.Add(generateTextLink("点击这里继续", 1));
            pushParagraph(p);
            await waitForTextLinkReturn();
            clearView();

            pushLine(">> 现在试试同时出现多个按钮.");
            for (int i = 0; i < 8; i++)
                pushLine("");
            p = new();
            p.Inlines.Add("   | ");
            for(int i = 0; i < 6; i++)
            {
                p.Inlines.Add(generateTextLink($"[ Button {i} ]", i));
                p.Inlines.Add(" | ");
            }
            p.Inlines.Add(generateTextLink($"[ Next ]", 42));
            pushParagraph(p);
            while (true)
            {
                linkReturn = await waitForTextLinkReturn(false);
                if (linkReturn != 42)
                    pushLine($"被按下的按钮编号是: [{linkReturn}].");
                else
                    break;
            }
            clearView();

            pushLine(">> 现在试试像 Era 一样的长文本模式.");
            for (int i = 0; i < 8; i++)
                pushLine("");
            p = new();
            p.Inlines.Add(" NyaScript 是一种以 ECMA Script 为参考设计的半函数式语言.");
            p.Inlines.Add(generateTextLink($"[ Continue ]", 0));
            pushParagraph(p);
            await waitForTextLinkReturn();
            p = new();
            p.Inlines.Add(" JavaScript 是 ECMA Script 的超集，同时具有 OO 和 FP 的特性.");
            p.Inlines.Add(generateTextLink($"[ Continue ]", 0));
            pushParagraph(p);
            await waitForTextLinkReturn();
            p = new();
            p.Inlines.Add(" 你所阅读的脚本运行在 .Net 动态运行时上.");
            p.Inlines.Add(generateTextLink($"[ Continue ]", 0));
            pushParagraph(p);
            await waitForTextLinkReturn();
            pushLine(" 不想写了总之这里是一大段文字.");
            for (int i = 0; i < 8; i++)
                pushLine("文字文字文字文字文字文字");
            p = new();
            p.Inlines.Add(generateTextLink($"[ Continue ]", 0));
            pushParagraph(p);
            await waitForTextLinkReturn();
            clearView();

            pushLine(">> 现在试试格式化文本.");
            for (int i = 0; i < 4; i++)
                pushLine("");
            PushFmtStrLn(
                "测试文本：\n" +
                "\\@<默认标签1>文本\\@<默认标签2>\\@<默认标签3>\\@<默认标签4>\n" +
                "\\@<赋值标签40, 40>\\@<赋值标签255, 255>\\@<赋值标签42(点击该标签退出), 42>"
                );
            while (true)
            {
                linkReturn = await waitForTextLinkReturn(false);
                if (linkReturn != 42)
                    pushLine($"被按下的按钮编号是: [{linkReturn}].");
                else
                    break;
            }
            clearView();
            

            pushLine(">> 测试程序结束.");
        }



        private string scriptFolderPath = ".\\Script";
        private string scriptBootFilePath = "..\\..\\..\\Script\\hello_world.nya";

        /// <summary>
        /// 从 scriptBootFilePath 配置位置启动脚本子线程
        /// </summary>
        private async void bootScript()
        {
            clearView();

            /* 异步纯粹为了动态效果，当然也有可能脚本特别大的时候可以阻止卡死 */

            FileProcessBar.IsIndeterminate = true;                 // 来点动态效果            
            await Task.Run(() => loadScript(scriptBootFilePath));  // 编译指定文件，编译后的文件储存在子线程 nyaScriptTask 中      
            FileProcessBar.IsIndeterminate = false;                // 关闭动态效果

            

            // 看看到底编译出来没有
            if (nyaScriptTask == null)
            {
                Console.WriteLine("[ BootScript ] : Failed.");
            }
            else
            {
                // 注册终止器
                nyaScriptCanceller.Token.Register(() => mainWindowWarningLog("CancelToken: 脚本线程终止信号已发送."));
                // 启动进程
                nyaScriptTask.Start();
                Console.WriteLine("[ BootScript ] : Script Start.");
            }
        }

        /// <summary>
        /// 如果 nyaScriptTask 存在，终止它并清除线程；否则什么也不做
        /// </summary>
        private void cancelScript()
        {
            if (nyaScriptTask != null)
            {
                nyaScriptCanceller.Cancel();
                // 由于协作终止的过程在等待输入的函数中，因此需要发送一个信号量激活函数
                if (!textLinkClickedSema.WaitOne(0)) textLinkClickedSema.Release();
                Thread.Sleep(500);
                textLinkClickedSema.WaitOne(0);

                nyaScriptCanceller.Dispose();
                nyaScriptTask.Dispose();
                nyaScriptTask = null;
                nyaScriptCanceller = new(); // 重建终止器
                return;
            }
        }

        #endregion



        #region 窗体控件方法

        public MainWindow()
        {
            // 在这句以后 xaml 中的对象才会生成，否则访问 xaml 中对象会返回 null
            InitializeComponent();

            // 预先创建控制台进程，这样之后子进程的控制台输出都将输出到这里
            ConsoleHelper.showConsole();
            ConsoleHelper.hideConsole();
        }

        // 在窗体元素渲染完成后进行的初始化操作 -----------------------------------
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            gameInit();
            nyaLangInit();
        }

        // 欢迎界面 ---------------------------------------------------------------

        private void Bootlink_Click(object sender, RoutedEventArgs e)
        {
            bootScript();
            return;

            //从文件中加载
            using (FileStream fs = new FileStream(".\\test.xaml", FileMode.Open, FileAccess.Read))
            {
                var tmp = XamlReader.Load(fs);
                Paragraph p = new();
                p.Inlines.Add((UIElement)tmp);
                GameView.Document.Blocks.Add(p);
            }

            /*从字符串中加载
            void LoadEmbeddedXaml()
            {
                Title = "Load Embedded Xaml";
                string strXaml = "<Button xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
                    " Foreground='LightSeaGreen' FontSize='16pt' Width='128' Height='32'>" +
                    " From String Object!</Button>";
                var t = XamlReader.Parse(strXaml);
                Paragraph p = new();
                p.Inlines.Add((UIElement)t);
                GameView.Document.Blocks.Add(p);
            }*/

            //bootScript();
        }

        // 菜单栏 -----------------------------------------------------------------

        private void ReloadScript_Click(object sender, RoutedEventArgs e)
        {
            cancelScript();
            bootScript();
        }

        // 功能键 -----------------------------------------------------------------

        // 功能键名称与返回值绑定
        private static readonly Dictionary<string, int> functionButtonReturn = 
            new(){
                {"F1Button", 301},
                {"F2Button", 302},
                {"F3Button", 303},
                {"F4Button", 304},
                {"F5Button", 305},
                {"F6Button", 306},
                {"F7Button", 307},
                {"F8Button", 308},

                {"SaveButton", 400},
                {"LoadButton", 401},
                {"QSaveButton", 402},
                {"QLoadButton", 403},

                {"ContinueButton", 500},
                {"AutoButton",     501},
                {"SkipButton",     502}
            };
        // 功能键按下动作
        private void FunctionButtion_Click(object sender, RoutedEventArgs e)
        {
            string senderName = ((Button)sender).Name; // 获取功能键名称
            int returnVal;
            if (functionButtonReturn.TryGetValue(senderName, out returnVal))
                setTextLinkClickReturn(returnVal);
            else
                mainWindowWarningLog($"已忽略未能识别的功能键 '{senderName}'.");            
        }

        // Debug 模式 -------------------------------------------------------------

        private void SetupDebugMode(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.showConsole(); // 显示控制台
            Console.WriteLine(
                "[ NOTE ] DO NOT CLOSE this window, or the game will CRASH.\n" +
                "         Uncheck the 'Debug' - 'DebugMode' option to hide debug console.\n" +
                "[ 注意 ] 请不要关闭此窗口，否则将终止游戏主进程.\n" +
                "         取消勾选 'Debug' - 'DebugMode' 选项来隐藏 Debug 控制台.\n\n");
        }
        private void ExitDebugMode(object sender, RoutedEventArgs e)
        {
            ConsoleHelper.hideConsole(); // 隐藏控制台
        }



        #endregion
    }
}
