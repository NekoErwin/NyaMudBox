/* 
 *   InteractRedirectInterface: 交互重定向接口 
 *       如果需要自定义某些方法实现，修改这个文件，
 *       并将对应方法的委托传入
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NyaLang.Core;

namespace NyaLang.Runtime
{
    public static class InteractRedirectInterface
    {

        /// <summary>
        /// 推送一行文字到重定向目标
        /// </summary>
        public static void PushLine(DynamicTypedef v)
        {
            if (PushLineMethod == null)
                NyaRuntimeWarning.Log("In static method [Redirect : $PushLine]: Method unregistered.");
            else
                PushLineMethod(v.ToString());
        }
        public static Action<string>? PushLineMethod;
        /// <summary>
        /// 推送格式化字符串到重定向目标
        /// </summary>
        public static void PushFormatLine(DynamicTypedef v)
        {
            if (PushFormatLineMethod == null)
                NyaRuntimeWarning.Log("In static method [Redirect : $PushFormatLine]: Method unregistered.");
            else
                PushFormatLineMethod(v.ToString());
        }
        public static Action<string>? PushFormatLineMethod;
        /// <summary>
        /// 等待重定向目标输入
        /// </summary>
        public static int WaitInput()
        {
            if (WaitInputMethod == null)
                NyaRuntimeWarning.Log("In static method [Redirect : $WaitInput]: Method unregistered.");
            else
                return WaitInputMethod();
            return -1;
        }
        public static Func<int>? WaitInputMethod;
        /// <summary>
        /// 输入一个数组，除非收到的值在数组中，否则继续等待输入
        /// </summary>
        public static int WaitValidInput(DynamicTypedef intTuple)
        {
            // 与 WaitInputMethod 使用的是同一个方法
            if (WaitInputMethod == null)
            {
                NyaRuntimeWarning.Log("In static method [Redirect : $WaitValidInput]: Method unregistered.");
                return -1;
            }

            // 类型判断
            if (intTuple.Value is DynamicTypedef[] intTuple_unbox)
            {
                // 提取所有合理的值
                int[] validInputList = new int[intTuple_unbox.Length];
                for (int i = 0; i < intTuple_unbox.Length; i++)
                    validInputList[i] = (int)(intTuple_unbox[i].Value);

                // 一直等到输入合适
                int mudBoxReturn;
                do
                {
                    mudBoxReturn = WaitInputMethod();
                } while (!validInputList.Contains(mudBoxReturn));
                return mudBoxReturn; // 最后返回
            }

            // 跳出上面分歧，则类型错误
            NyaRuntimeWarning.Log("In static method [Redirect : $WaitValidInput]: Param is not a int-array.");
            return -1;
        }
        /// <summary>
        /// 清空重定向目标的输出内容
        /// </summary>
        public static void ClearView()
        {
            if (ClearViewMethod == null)
                NyaRuntimeWarning.Log("In static method [Redirect : $ClearView]: Method unregistered.");
            else
                ClearViewMethod();
        }
        public static Action? ClearViewMethod;


        /* MudBox 私有 */

        // 将已显示在屏幕上的文本按钮失效
        public static void DisableAllTextLink()
        {
            if (DisableAllTextLinkMethod == null)
                NyaRuntimeWarning.Log("In static method [Redirect : $DisableAllTextLink]: Method unregistered.");
            else
                DisableAllTextLinkMethod();
        }
        public static Action? DisableAllTextLinkMethod;

    }
}
