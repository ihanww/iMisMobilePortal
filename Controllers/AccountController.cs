using iMis.Core;
using iMis.Core.Infrastructure;
using iMis.Core.MVC;
using iMis.Core.Security;
using iMis.Core.Serviecs;
using iMis.Core.Utility;
using iMisMobilePortal.UserAuthentication;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace iMisMobilePortal.Controllers
{
    public class AccountController : BaseController
    {

        #region [Field] 基本设置
        /// <summary>
        /// 客户登陆启用设置
        /// </summary>
        public static string CustomerLoginState = ConfigurationManager.AppSettings["CustomerLoginState"];

        /// <summary>
        /// 界面系统标题
        /// </summary>
        public static string ImisUiTitle = BaseContext.RootFolder.ToUpper();
        #endregion

        /// <summary>
        /// 默认加载
        /// </summary>
        /// <returns></returns>
        [HttpGet, AllowAnonymous]
        public ActionResult Login() {
            //清理 Session 缓存值,避免登陆失败. 
            //Session.RemoveAll();

            var userAuthentication = new UserAuthentication.UserAuthenticationSoapClient();
            var strCompanyAuthUrlList = userAuthentication.GetCompanyAuthUrlList();

            var lstCompanyAuthUrl = strCompanyAuthUrlList?.FromJson<List<CompanyAuthUrl>>() ?? new List<CompanyAuthUrl>();
            ViewBag.CompanyAuthUrl = lstCompanyAuthUrl.ToSelectList(item => item.CompanyName, item => item.CompanyCode);
            ViewBag.CompanyAuthUrlList = lstCompanyAuthUrl.ToJsonKeyValue("CompanyCode", new[] { "LoginUrl" });

            return View(new LoginUserInfo());
        }


        /// <summary>
        /// 测试ItControl页面,提交按钮
        /// </summary>
        /// <returns></returns>
        [HttpPost, AllowAnonymous, AllowCors]
        public ActionResult Login(LoginUserInfo userInfo) {

            //清理 Session 缓存值,避免登陆失败. 
            //Session.RemoveAll();

            if(userInfo.UserName.IsEmpty()) {
                return new AjaxJsonResult<object> {
                    Success = false,
                    Message = "用户名不能为空!"
                };
            }

            if(userInfo.Password.IsEmpty()) {
                return new AjaxJsonResult<object> {
                    Success = false,
                    Message = "密码不能为空!"
                };
            }

            //是否为测试机
            var isDebug = IsDebugging();

            var userService = new UserService();
            var validPass = false;
            //string userRole = null;
            var strMessage = string.Empty;

            //获取用户验证操作适配器
            var userAuthentication = new UserAuthentication.UserAuthenticationSoapClient();
            var strCompanyAuthUrlList = userAuthentication.GetCompanyAuthUrlList();
            var lstCompanyAuthUrl = strCompanyAuthUrlList?.FromJson<List<CompanyAuthUrl>>() ?? new List<CompanyAuthUrl>();
            var companyAuthInfo = lstCompanyAuthUrl.FirstOrDefault(addr => addr.CompanyCode == userInfo.CompanyCode);
            if(companyAuthInfo == null) {
                return new AjaxJsonResult<object> {
                    Success = false,
                    Message = "无法匹配跳转的公司代码!"
                };
            }

            //用户登陆验证
            if(!isDebug && !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["UserAuthentication_AccessToken"])) {

                //尝试使用OA用户登陆验证
                var strAccessToken = ConfigurationManager.AppSettings["UserAuthentication_AccessToken"];
                var strAuthType = ConfigurationManager.AppSettings["UserAuthentication_Type"] ?? "OA";

                //获取用户验证对象
                AuthenticationResult userAuthResult;
                switch(strAuthType) {
                    case "AD":
                        userAuthResult = userAuthentication.ADUserAuthentication(strAccessToken, userInfo.UserName, userInfo.Password);
                        break;

                    default:
                        userAuthResult = userAuthentication.OAUserAuthentication(strAccessToken, userInfo.UserName, userInfo.Password);
                        break;
                }

                if(userAuthResult.Succeed) {
                    //转换为OA返回用户身份
                    //userRole = RoleNames.User;
                    userInfo.UserName = userAuthResult.Result;
                    userService.UpdateUserInfo(userInfo.UserName, userInfo.Password);
                    validPass = true;
                } else {
                    //判断是否被锁定用户,有锁定就提醒.
                    if(userAuthResult.ErrorCode.Equals(2)) {
                        return new AjaxJsonResult<object> {
                            Success = false,
                            Message = userAuthResult.Message
                        };
                    }
                }
            }

            if(!isDebug && !validPass) {
                //尝试使用本地用户验证,忽略验证失败情况
                if(userService.UserLogin(userInfo.UserName, userInfo.Password, out string message)) {
                    //userRole = RoleNames.User;
                    validPass = true;
                } else {
                    if(message.IsNotEmpty()) strMessage = message;
                }
            }

            //外部客户登陆启用设置
            CustomerLoginState = CustomerLoginState ?? "false";
            if(!isDebug && !validPass && CustomerLoginState.Contains("true")) {
                //尝试使用外部客户登陆,忽略验证失败情况
                if(userService.CustomerLogin(userInfo.UserName, userInfo.Password, out string message, out bool isfrist)) {
                    //userRole = RoleNames.Customer;
                    validPass = true;
                } else {
                    if(message.IsNotEmpty()) strMessage = message;
                }
            }

            //当前为测试状态或者验证通过时,还原用户身份信息
            if(isDebug || validPass) {

                //缓存用户登陆身份
                //var userId = userInfo.UserName;
                //var identity = new GenericIdentity(userId);
                //HttpContext.Response.SetAuthCookie(userId);
                //HttpContext.User = new GenericPrincipal(identity, new[] { userRole ?? RoleNames.Guest });
                //Session.GetUserInfo();

                //获取二次认证令牌
                var token = MemcachedHelper.SetSsoUserId(userInfo.UserName);

                //companyAuthInfo.HomePage = companyAuthInfo.HomePage.IndexOf("?", StringComparison.Ordinal) >= 0 ?
                //    $"{companyAuthInfo.HomePage}&token={token}" :
                //    $"{companyAuthInfo.HomePage}?token={token}";

                var restoreUrl = $"http://{this.Request.Url?.Host}/{BaseContext.RootFolder}/Account/Restore";
                restoreUrl = $"{restoreUrl}?relogin=true&token={token}&rurl={this.Server.UrlEncode(companyAuthInfo.HomePage)}";

                return new AjaxJsonResult<object> {
                    Success = true,
                    Url = restoreUrl // companyAuthInfo.HomePage
                };
            }

            if(strMessage.IsEmpty()) strMessage = "密码错误或用户不存在.";
            return new AjaxJsonResult<object> {
                Success = false,
                Message = strMessage
            };
        }

    }


    #region [LoginUserInfo] 登陆用户提交信息

    /// <summary>
    /// 登陆用户提交信息
    /// </summary>
    public class LoginUserInfo
    {
        /// <summary>
        /// 公司代码
        /// </summary>
        public string CompanyCode { get; set; }

        /// <summary>
        /// 登陆用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 登陆密码
        /// </summary>
        public string Password { get; set; }

    }

    /// <summary>
    /// 工厂用户认证入口
    /// </summary>
    public class CompanyAuthUrl
    {
        /// <summary>
        /// 公司代码
        /// </summary>
        public string CompanyCode { get; set; }

        /// <summary>
        /// 公司名称
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// 默认跳转页面Url
        /// </summary>
        public string HomePage { get; set; }

        /// <summary>
        /// 登陆认证接口Url
        /// </summary>
        public string LoginUrl { get; set; }
    }

    #endregion
}