using System.Net;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class ClientViewManager : MonoBehaviour
{
    // Start is called before the first frame update
    TextField addressInputField;
    TextField portInputField;
    TextField nameInputField;
    Button okButton;
    Button quitButton;
    Label tipLabel;
    Label portPlaceholder;

    UnityTransport transport;
    ushort defaultPort = 7777;
    bool connecting;
    bool destroyed;

    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        addressInputField = root.Q<TextField>("address");
        portInputField = root.Q<TextField>("port");
        nameInputField = root.Q<TextField>("name");
        okButton = root.Q<Button>("ok");
        quitButton = root.Q<Button>("quit");
        tipLabel = root.Q<Label>("tip");
        portPlaceholder = root.Q<Label>("portPlaceholder");

        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // 端口留空时用场景里给 UnityTransport 配的默认端口
        defaultPort = transport.ConnectionData.Port;
        if (defaultPort == 0) defaultPort = 7777;
        // 2022.3 的 TextField 还没有 placeholder 接口，这里用叠一层的 Label 当占位符
        portPlaceholder.text = defaultPort.ToString();

        okButton.clicked += Connect;
        quitButton.clicked += () =>
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
            SceneManager.LoadScene("start");
        };
        nameInputField.RegisterValueChangedCallback((evt) =>
        {
            clientName = evt.newValue;
        });
        addressInputField.RegisterValueChangedCallback((evt) =>
        {
            // 地址里自带端口（如 http://example.com:7788）时同步到端口栏，让玩家看到实际用的端口
            if (ServerAddress.TryParse(evt.newValue, out _, out var urlPort, out _) && urlPort.HasValue)
            {
                portInputField.SetValueWithoutNotify(urlPort.Value.ToString());
            }
            RefreshInputState();
        });
        portInputField.RegisterValueChangedCallback((evt) =>
        {
            RefreshInputState();
        });
        RefreshInputState();
    }

    void RefreshInputState()
    {
        var addressEmpty = string.IsNullOrWhiteSpace(addressInputField.value);
        var portEmpty = string.IsNullOrWhiteSpace(portInputField.value);
        var addressOk = ServerAddress.TryParse(addressInputField.value, out _, out _, out var addressError);
        var portOk = ServerAddress.TryParsePort(portInputField.value, out _, out var portError);

        SetFieldState(addressInputField, addressEmpty, addressOk);
        SetFieldState(portInputField, portEmpty, portOk);
        portPlaceholder.style.display = portEmpty ? DisplayStyle.Flex : DisplayStyle.None;

        if (!addressEmpty && !addressOk) tipLabel.text = addressError;
        else if (!portEmpty && !portOk) tipLabel.text = portError;
        else tipLabel.text = string.Empty;

        okButton.SetEnabled(addressOk && portOk && !connecting);
    }

    static void SetFieldState(TextField field, bool empty, bool valid)
    {
        if (empty) field.style.borderBottomColor = StyleKeyword.Null;
        else field.style.borderBottomColor = valid ? Color.green : Color.red;
    }

    async void Connect()
    {
        if (connecting) return;

        if (!ServerAddress.TryParse(addressInputField.value, out var host, out var addressPort, out var error))
        {
            tipLabel.text = error;
            RefreshInputState();
            return;
        }
        if (!ServerAddress.TryParsePort(portInputField.value, out var fieldPort, out error))
        {
            tipLabel.text = error;
            RefreshInputState();
            return;
        }

        // 端口栏 > 地址里写的端口 > 默认端口
        var port = fieldPort ?? addressPort ?? defaultPort;

        connecting = true;
        RefreshInputState();
        tipLabel.text = "正在解析地址…";

        try
        {
            var address = host;
            if (!IPAddress.TryParse(host, out _))
            {
                var resolved = await ResolveHostAsync(host);
                if (destroyed) return;
                if (resolved == null)
                {
                    connecting = false;
                    RefreshInputState();
                    tipLabel.text = $"无法解析地址 {host}";
                    return;
                }
                address = resolved;
            }

            if (NetworkManager.Singleton == null) return;

            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;

            if (NetworkManager.Singleton.StartClient()) tipLabel.text = $"正在连接 {host}:{port}…";
            else
            {
                connecting = false;
                RefreshInputState();
                tipLabel.text = "连接失败，请检查地址和端口";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            if (destroyed) return;
            connecting = false;
            RefreshInputState();
            tipLabel.text = "连接失败，请检查地址和端口";
        }
    }

    // 放到后台线程查 DNS，避免域名解析卡住界面
    static Task<string> ResolveHostAsync(string host)
    {
        return Task.Run(() => ServerAddress.Resolve(host));
    }

    void OnDestroy()
    {
        destroyed = true;
    }

    public static string clientName = "房主";
}
