using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class HPHandler : NetworkBehaviour
{
    [Networked(OnChanged = nameof(OnHPChanged))]
    byte HP { get; set; }

    [Networked(OnChanged = nameof(OnStateChanged))]
    public bool IsDead { get; set; }

    bool isInitialized = false;

    const byte startingHP = 5;

    public Color uiOnHitColor;
    public Image uiOnHitImage;

    public MeshRenderer bodyMeshRenderer;
    Color defaultMeshBodyColor;

    public GameObject playerModel;
    public GameObject deathGameObjectPrefab;

    //Other components
    HitboxRoot hitboxRoot;
    CharacterMovementHandler characterMovementHandler;

    private void Awake()
    {
        characterMovementHandler = GetComponent<CharacterMovementHandler>();
        hitboxRoot = GetComponentInChildren<HitboxRoot>();
    }

    // Start is called before the first frame update
    void Start()
    {
        HP = startingHP;
        IsDead = false;

        defaultMeshBodyColor = bodyMeshRenderer.material.color;

        isInitialized = true;
    }

    IEnumerator OnHitCO()
    {
        bodyMeshRenderer.material.color = Color.white;

        if (Object.HasInputAuthority)
            uiOnHitImage.color = uiOnHitColor;

        yield return new WaitForSeconds(0.2f);

        bodyMeshRenderer.material.color = defaultMeshBodyColor;

        if (Object.HasInputAuthority && !IsDead)
            uiOnHitImage.color = new Color(0, 0, 0, 0);
    }

    IEnumerator ServerReviveCO()
    {
        yield return new WaitForSeconds(2.0f);

        characterMovementHandler.RequestRespawn();
    }


    //Function only called on the server
    public void OnTakeDamage()
    {
        //Only take damage while alive
        if (IsDead)
            return;

        HP -= 1;

        Debug.Log($"{Time.time} {transform.name} took damage got {HP} left ");

        //Player died
        if (HP <= 0)
        {
            Debug.Log($"{Time.time} {transform.name} died");

            StartCoroutine(ServerReviveCO());

            IsDead = true;
        }
    }

    static void OnHPChanged(Changed<HPHandler> changed)
    {
        Debug.Log($"{Time.time} OnHPChanged value {changed.Behaviour.HP}");

        byte newHP = changed.Behaviour.HP;

        //Load the old value
        changed.LoadOld();

        byte oldHP = changed.Behaviour.HP;

        //Check if the HP has been decreased
        if (newHP < oldHP)
            changed.Behaviour.OnHPReduced();
    }

    private void OnHPReduced()
    {
        if (!isInitialized)
            return;

        StartCoroutine(OnHitCO());
    }

    static void OnStateChanged(Changed<HPHandler> changed)
    {
        Debug.Log($"{Time.time} OnStateChanged isDead {changed.Behaviour.IsDead}");

        bool isDeadCurrent = changed.Behaviour.IsDead;

        //Load the old value
        changed.LoadOld();

        bool isDeadOld = changed.Behaviour.IsDead;

        //Handle on death for the player. Also check if the player was dead but is now alive in that case revive the player.
        if (isDeadCurrent)
            changed.Behaviour.OnDeath();
        else if (!isDeadCurrent && isDeadOld)
            changed.Behaviour.OnRevive();
    }

    private void OnDeath()
    {
        Debug.Log($"{Time.time} OnDeath");

        playerModel.gameObject.SetActive(false);
        hitboxRoot.HitboxRootActive = false;
        characterMovementHandler.SetCharacterControllerEnabled(false);

        Instantiate(deathGameObjectPrefab, transform.position, Quaternion.identity);
    }

    private void OnRevive()
    {
        Debug.Log($"{Time.time} OnRevive");

        if (Object.HasInputAuthority)
            uiOnHitImage.color = new Color(0, 0, 0, 0);

        playerModel.gameObject.SetActive(true);
        hitboxRoot.HitboxRootActive = true;
        characterMovementHandler.SetCharacterControllerEnabled(true);
    }

    public void OnRespawned()
    {
        //Reset variables
        HP = startingHP;
        IsDead = false;
    }
}
