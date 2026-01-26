using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientVieManager : MonoBehaviour
{
    // Start is called before the first frame update
    TextField ipInputField;
    TextField nameInputField;
    Button okButton;
    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        ipInputField = root.Q<TextField>("ip");
        nameInputField = root.Q<TextField>("name");
        okButton = root.Q<Button>("ok");
        okButton.SetEnabled(false);
        okButton.clicked += () =>
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address = ipInputField.value;
            NetworkManager.Singleton.StartClient();
        };
        nameInputField.RegisterValueChangedCallback((evt) =>
        {
            clientName = evt.newValue;
        });
        ipInputField.RegisterValueChangedCallback((evt) =>
        {
            if (IPAddress.TryParse(evt.newValue, out var address))
            {
                ipInputField.style.borderBottomColor = Color.green;
                okButton.SetEnabled(true);
            }
            else
            {
                ipInputField.style.borderBottomColor = Color.red;
                okButton.SetEnabled(false);
            }
        });
    }

    public static string clientName = "·¿Ö÷";
}
