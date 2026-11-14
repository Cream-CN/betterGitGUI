using System;
using System.IO;
using System.Text;

namespace betterGitGUI
{
    /// <summary>
    /// 远程地址的两种常见写法。
    /// </summary>
    public enum RemoteProtocol
    {
        Unknown,
        Ssh,
        Https,
    }

    /// <summary>
    /// 拆开后的远程地址：主机、端口、路径（owner/repo.git）。
    /// </summary>
    public class RemoteUrlInfo
    {
        public RemoteProtocol Protocol = RemoteProtocol.Unknown;
        public string Host = "";
        public string Port = "";
        public string Path = "";
        public string User = "";
        public bool HadCredentials;

        /// <summary>给人看的协议名。</summary>
        public string ProtocolName
        {
            get
            {
                if (Protocol == RemoteProtocol.Ssh) return "SSH";
                if (Protocol == RemoteProtocol.Https) return "HTTPS";
                return "未知";
            }
        }
    }

    /// <summary>
    /// 把 git 远程地址在 SSH 与 HTTPS 两种写法之间转换。
    /// 支持：
    ///   git@github.com:owner/repo.git        （scp 写法）
    ///   ssh://git@host:port/owner/repo.git
    ///   https://host/owner/repo.git          （可带账号密码）
    /// 本地路径、file:// 远程不转换，直接报错。
    /// </summary>
    public static class RemoteUrl
    {
        public static RemoteProtocol Detect(string url)
        {
            RemoteUrlInfo info;
            string error;
            if (!TryParse(url, out info, out error)) return RemoteProtocol.Unknown;
            return info.Protocol;
        }

        public static bool TryParse(string url, out RemoteUrlInfo info, out string error)
        {
            info = new RemoteUrlInfo();
            error = null;

            var raw = (url ?? "").Trim();
            if (raw.Length == 0)
            {
                error = "远程地址为空";
                return false;
            }

            // 本地路径 / file 协议：没有主机名，无法在 ssh 与 https 之间转换
            var looksLikeLocalPath =
                raw.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("\\\\", StringComparison.Ordinal)
                || raw.StartsWith("/", StringComparison.Ordinal)
                || (raw.Length >= 2 && char.IsLetter(raw[0]) && raw[1] == ':' && !raw.Contains("@"));
            if (looksLikeLocalPath)
            {
                error = "这是本地路径或 file:// 远程，无法转换成网络地址";
                return false;
            }

            if (raw.IndexOf("://", StringComparison.Ordinal) > 0)
            {
                Uri uri;
                if (!Uri.TryCreate(raw, UriKind.Absolute, out uri))
                {
                    error = "无法解析这个地址";
                    return false;
                }

                var scheme = uri.Scheme.ToLowerInvariant();
                if (scheme != "http" && scheme != "https" && scheme != "ssh" && scheme != "git")
                {
                    error = "暂不支持 " + scheme + ":// 形式的远程";
                    return false;
                }

                info.Protocol = (scheme == "http" || scheme == "https") ? RemoteProtocol.Https : RemoteProtocol.Ssh;
                info.Host = uri.Host;
                if (!uri.IsDefaultPort) info.Port = uri.Port.ToString();
                info.Path = uri.AbsolutePath.TrimStart('/');
                if (info.Protocol == RemoteProtocol.Ssh && !string.IsNullOrEmpty(uri.UserInfo))
                    info.User = uri.UserInfo.Split(':')[0];
                if (info.Protocol == RemoteProtocol.Https && !string.IsNullOrEmpty(uri.UserInfo))
                    info.HadCredentials = true;
            }
            else
            {
                // scp 写法 user@host:path
                var at = raw.IndexOf('@');
                var colon = raw.IndexOf(':');
                if (at <= 0 || colon <= at)
                {
                    error = "无法识别这个地址（既不是 https:// 也不是 user@host:path）";
                    return false;
                }
                info.Protocol = RemoteProtocol.Ssh;
                info.User = raw.Substring(0, at);
                info.Host = raw.Substring(at + 1, colon - at - 1);
                info.Path = raw.Substring(colon + 1).TrimStart('/');
            }

            info.Path = info.Path.TrimEnd('/');
            if (info.Host.Length == 0)
            {
                error = "地址里没有主机名";
                return false;
            }
            if (info.Path.Length == 0)
            {
                error = "地址里没有仓库路径（owner/repo）";
                return false;
            }
            if (info.Host.StartsWith(".") || info.Path.Contains(".."))
            {
                error = "地址格式不正确";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 转成 SSH 写法：22 端口用 scp 形式，其它端口用 ssh:// 形式。
        /// </summary>
        public static bool TryToSsh(string url, out string result, out string error)
        {
            result = null;
            RemoteUrlInfo info;
            if (!TryParse(url, out info, out error)) return false;

            var user = string.IsNullOrEmpty(info.User) ? "git" : info.User;
            if (string.IsNullOrEmpty(info.Port) || info.Port == "22")
                result = user + "@" + info.Host + ":" + info.Path;
            else
                result = "ssh://" + user + "@" + info.Host + ":" + info.Port + "/" + info.Path;
            return true;
        }

        /// <summary>
        /// 转成 HTTPS 写法。只有在原地址本身就是 https 时才保留自定义端口，
        /// 因为 SSH 的自定义端口和 HTTPS 端口不是一回事。
        /// </summary>
        public static bool TryToHttps(string url, out string result, out string error)
        {
            result = null;
            RemoteUrlInfo info;
            if (!TryParse(url, out info, out error)) return false;

            var port = "";
            if (info.Protocol == RemoteProtocol.Https && !string.IsNullOrEmpty(info.Port))
                port = ":" + info.Port;

            result = "https://" + info.Host + port + "/" + info.Path;
            return true;
        }

        /// <summary>
        /// 转成当前协议的另一种写法。已经是 SSH 就转 HTTPS，反之亦然。
        /// </summary>
        public static bool TryConvertToOther(string url, out string result, out string error)
        {
            result = null;
            error = null;

            RemoteUrlInfo info;
            if (!TryParse(url, out info, out error)) return false;

            if (info.Protocol == RemoteProtocol.Ssh) return TryToHttps(url, out result, out error);
            if (info.Protocol == RemoteProtocol.Https) return TryToSsh(url, out result, out error);

            error = "无法识别当前远程的协议";
            return false;
        }
    }
}
