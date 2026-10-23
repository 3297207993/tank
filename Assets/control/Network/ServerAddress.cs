using System;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// 解析玩家填写的服务器地址。
/// 支持这些写法：192.168.1.10、example.com、example.com:7788、
/// http://example.com:7788/path、ws://example.com、[::1]:7788。
/// 协议头和路径只是为了方便直接粘贴链接，连接时用不到，会被丢掉。
/// </summary>
public static class ServerAddress
{
    /// <summary>
    /// 解析地址栏。成功时 host 是不含端口的主机名或 IP，
    /// port 是地址里写明的端口，没有写就是 null。
    /// </summary>
    public static bool TryParse(string input, out string host, out ushort? port, out string error)
    {
        host = null;
        port = null;
        error = null;

        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "请输入服务器地址";
            return false;
        }

        // 去掉协议头（http://、wss://、udp:// 之类），再去掉路径和查询串
        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0) text = text.Substring(schemeEnd + 3);
        var pathStart = text.IndexOfAny(new[] { '/', '?', '#' });
        if (pathStart >= 0) text = text.Substring(0, pathStart);
        text = text.Trim();
        if (text.Length == 0)
        {
            error = "地址格式不正确";
            return false;
        }

        string portText = null;
        if (text[0] == '[')
        {
            // [IPv6] 或 [IPv6]:端口
            var close = text.IndexOf(']');
            if (close < 0)
            {
                error = "IPv6 地址缺少 ]";
                return false;
            }
            host = text.Substring(1, close - 1);
            var rest = text.Substring(close + 1);
            if (rest.Length > 0)
            {
                if (rest[0] != ':')
                {
                    error = "地址格式不正确";
                    return false;
                }
                portText = rest.Substring(1);
            }
        }
        else
        {
            var firstColon = text.IndexOf(':');
            var lastColon = text.LastIndexOf(':');
            if (firstColon >= 0 && firstColon == lastColon)
            {
                // 只有一个冒号，按 主机:端口 处理
                host = text.Substring(0, firstColon);
                portText = text.Substring(firstColon + 1);
            }
            else
            {
                // 没有冒号，或者是没加方括号的 IPv6，整体当作主机
                host = text;
            }
        }

        host = host.Trim();
        if (host.Length == 0 || !IsValidHost(host))
        {
            error = "地址格式不正确";
            return false;
        }

        if (portText != null)
        {
            if (!TryParsePort(portText, out var parsedPort, out error)) return false;
            port = parsedPort;
        }

        return true;
    }

    /// <summary>
    /// 解析端口栏。留空表示使用默认端口，返回 true 且 port 为 null。
    /// </summary>
    public static bool TryParsePort(string input, out ushort? port, out string error)
    {
        port = null;
        error = null;

        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0) return true;

        if (!ushort.TryParse(text, out var value) || value == 0)
        {
            error = "端口应为 1-65535 之间的数字";
            return false;
        }

        port = value;
        return true;
    }

    static bool IsValidHost(string host)
    {
        if (IPAddress.TryParse(host, out _)) return true;
        if (host.Length > 253) return false;

        foreach (var label in host.Split('.'))
        {
            if (label.Length == 0 || label.Length > 63) return false;
            if (label[0] == '-' || label[label.Length - 1] == '-') return false;
            foreach (var c in label)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 把主机名解析成 IP。UTP 1.x 不会自己查 DNS，所以域名要先在这里解析。
    /// 优先返回 IPv4，只有 IPv6 时才返回 IPv6；解析失败返回 null。
    /// </summary>
    public static string Resolve(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            string ipv6 = null;
            foreach (var address in addresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork) return address.ToString();
                if (ipv6 == null && address.AddressFamily == AddressFamily.InterNetworkV6) ipv6 = address.ToString();
            }
            return ipv6;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
