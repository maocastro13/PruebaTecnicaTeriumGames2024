using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.SceneManagement;

public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
{
    public TextMeshProUGUI playerNickNameTM;
    public static NetworkPlayer Local { get; set; }
    public Transform playerModel;

    [Networked(OnChanged = nameof(OnNickNameChanged))]
    public NetworkString<_16> nickName { get; set; }

    // Remote Client Token Hash
    [Networked] public int token { get; set; }

    bool isPublicJoinMessageSent = false;

    public LocalCameraHandler localCameraHandler;
    public GameObject localUI;

    //Camera mode
    public bool is3rdPersonCamera { get; set; }

    //Other components
    NetworkInGameMessages networkInGameMessages;

    void Awake()
    {
        networkInGameMessages = GetComponent<NetworkInGameMessages>();
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    public override void Spawned()
    {
        bool isReadyScene = SceneManager.GetActiveScene().name == "Ready";

        if (Object.HasInputAuthority)
        {
            Local = this;

            if (isReadyScene)
            {
                Camera.main.transform.position = new Vector3(transform.position.x, Camera.main.transform.position.y, Camera.main.transform.position.z);

                //Disable local camera
                localCameraHandler.gameObject.SetActive(false);

                //Disable UI for local player
                localUI.SetActive(false);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                //Sets the layer of the local players model
                Utils.SetRenderLayerInChildren(playerModel, LayerMask.NameToLayer("LocalPlayerModel"));

                //Disable main camera
                if (Camera.main != null)
                    Camera.main.gameObject.SetActive(false);

                //Enable the local camera
                localCameraHandler.localCamera.enabled = true;
                localCameraHandler.gameObject.SetActive(true);


                //Detach camera if enabled
                localCameraHandler.transform.parent = null;

                //Enable UI for local player
                localUI.SetActive(true);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            RPC_SetNickName(GameManager.instance.playerNickName);

            //Disable the nick name for the local player.
            playerNickNameTM.gameObject.SetActive(false);

            Debug.Log("Spawned local player");
        }
        else
        {
            //Disable the local camera for remote players
            localCameraHandler.localCamera.enabled = false;
            localCameraHandler.gameObject.SetActive(false);

            //Disable UI for remote player
            localUI.SetActive(false);

            Debug.Log($"{Time.time} Spawned remote player");  
        }

        //Set the Player as a player object
        Runner.SetPlayerObject(Object.InputAuthority, Object);

        //Make it easier to tell which player is which.
        transform.name = $"P_{Object.Id}";
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerLeftNetworkObject))
            {
                if (playerLeftNetworkObject == Object)
                    Local.GetComponent<NetworkInGameMessages>().SendInGameRPCMessage(playerLeftNetworkObject.GetComponent<NetworkPlayer>().nickName.ToString(), "left");
            }

        }


        if (player == Object.InputAuthority)
            Runner.Despawn(Object);

    }
    static void OnNickNameChanged(Changed<NetworkPlayer> changed)
    {
        Debug.Log($"{Time.time} OnHPChanged value {changed.Behaviour.nickName}");

        changed.Behaviour.OnNickNameChanged();
    }

    private void OnNickNameChanged()
    {
        Debug.Log($"Nickname changed for player to {nickName} for player {gameObject.name}");

        playerNickNameTM.text = nickName.ToString();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetNickName(string nickName, RpcInfo info = default)
    {
        Debug.Log($"[RPC] SetNickName {nickName}");
        this.nickName = nickName;

        if (!isPublicJoinMessageSent)
        {
            networkInGameMessages.SendInGameRPCMessage(nickName, "joined");

            isPublicJoinMessageSent = true;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetCameraMode(bool is3rdPersonCamera, RpcInfo info = default)
    {
        Debug.Log($"[RPC] SetCameraMode. is3rdPersonCamera  {is3rdPersonCamera}");

        this.is3rdPersonCamera = is3rdPersonCamera;
    }

    void OnDestroy()
    {
        //Get rid of the local camera if we get destroyed as a new one will be spawned with the new Network player
        if (localCameraHandler != null)
            Destroy(localCameraHandler.gameObject);

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"{Time.time} OnSceneLoaded: " + scene.name);

        if (scene.name != "Ready")
        {
            //Tell the host that we need to perform the spawned code manually. 
            if (Object.HasStateAuthority && Object.HasInputAuthority)
                Spawned();

            if (Object.HasStateAuthority)
                GetComponent<CharacterMovementHandler>().RequestRespawn();
        }
    }
}
