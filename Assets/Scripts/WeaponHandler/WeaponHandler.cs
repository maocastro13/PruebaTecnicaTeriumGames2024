using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class WeaponHandler : NetworkBehaviour
{
    [Header("Effects")]
    public ParticleSystem fireParticleSystem;
    public ParticleSystem fireParticleSystemRemotePlayer;

    [Header("Aim")]
    public Transform aimPoint;

    [Header("Collision")]
    public LayerMask collisionLayers;


    [Networked(OnChanged = nameof(OnFireChanged))]
    public bool isFiring { get; set; }


    float lastTimeFired = 0;

    float maxHitDistance = 200;

    //Timing
    TickTimer grenadeFireDelay = TickTimer.None;
    TickTimer rocketFireDelay = TickTimer.None;

    //Other components
    HPHandler hpHandler;
    NetworkPlayer networkPlayer;
    NetworkObject networkObject;

    private void Awake()
    {
        hpHandler = GetComponent<HPHandler>();
        networkPlayer = GetBehaviour<NetworkPlayer>();
        networkObject = GetComponent<NetworkObject>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public override void FixedUpdateNetwork()
    {
        if (hpHandler.isDead)
            return;

        //Get the input from the network
        if (GetInput(out NetworkInputData networkInputData))
        {
            if (networkInputData.isFireButtonPressed)
                Fire(networkInputData.aimForwardVector, networkInputData.cameraPosition);
        }
    }

    void Fire(Vector3 aimForwardVector, Vector3 cameraPosition)
    {
        //Limit fire rate
        if (Time.time - lastTimeFired < 0.15f)
            return;

        StartCoroutine(FireEffectCO());

        HPHandler hitHPHandler = CalculateFireDirection(aimForwardVector, cameraPosition, out Vector3 fireDirection);

        if(hitHPHandler != null && Object.HasStateAuthority)
            hitHPHandler.OnTakeDamage(networkPlayer.nickName.ToString(), 1); ;

        lastTimeFired = Time.time;
    }

    HPHandler CalculateFireDirection(Vector3 aimForwardVector, Vector3 cameraPosition, out Vector3 fireDirection)
    {
        LagCompensatedHit hitinfo = new LagCompensatedHit();

        fireDirection = aimForwardVector;
        float hitDistance = maxHitDistance;

        //Do a raycast from the 3rd person camera
        if (networkPlayer.is3rdPersonCamera)
        {
            Runner.LagCompensation.Raycast(cameraPosition, fireDirection, hitDistance, Object.InputAuthority, out hitinfo, collisionLayers, HitOptions.IgnoreInputAuthority | HitOptions.IncludePhysX);

            //Check against other players
            if (hitinfo.Hitbox != null)
            {
                fireDirection = (hitinfo.Point - aimPoint.position).normalized;
                hitDistance = hitinfo.Distance;

                Debug.DrawRay(cameraPosition, aimForwardVector * hitDistance, new Color(0.4f, 0, 0), 1);
            }
            //Check aginst PhysX colliders if we didn't hit a player
            else if (hitinfo.Collider != null)
            {
                fireDirection = (hitinfo.Point - aimPoint.position).normalized;
                hitDistance = hitinfo.Distance;

                Debug.DrawRay(cameraPosition, aimForwardVector * hitDistance, new Color(0, 0.4f, 0), 1);
            }
            else
            {
                Debug.DrawRay(cameraPosition, fireDirection * hitDistance, Color.gray, 1);

                fireDirection = ((cameraPosition + fireDirection * hitDistance) - aimPoint.position).normalized;
            }
        }

        //Reset hit distance
        hitDistance = maxHitDistance;

        //Check if we hit anything with the fire
        Runner.LagCompensation.Raycast(aimPoint.position, fireDirection, maxHitDistance, Object.InputAuthority, out hitinfo, collisionLayers, HitOptions.IgnoreInputAuthority | HitOptions.IncludePhysX);

        //Check against other players
        if (hitinfo.Hitbox != null)
        {
            hitDistance = hitinfo.Distance;
            HPHandler hitHPHandler = null;

            if (Object.HasStateAuthority)
            {
                hitHPHandler = hitinfo.Hitbox.transform.root.GetComponent<HPHandler>();
                Debug.DrawRay(aimPoint.position, fireDirection * hitDistance, Color.red, 1);

                return hitHPHandler;
            }
        }
        //Check aginst PhysX colliders if we didn't hit a player
        else if (hitinfo.Collider != null)
        {
            hitDistance = hitinfo.Distance;

            Debug.DrawRay(aimPoint.position, fireDirection * hitDistance, Color.green, 1);
        }
        else Debug.DrawRay(aimPoint.position, fireDirection * hitDistance, Color.black, 1);

        return null;
    }


    IEnumerator FireEffectCO()
    {
        isFiring = true;

        if (networkPlayer.is3rdPersonCamera)
            fireParticleSystemRemotePlayer.Play();
        else fireParticleSystem.Play();


        yield return new WaitForSeconds(0.09f);

        isFiring = false;
    }


    static void OnFireChanged(Changed<WeaponHandler> changed)
    {
        //Debug.Log($"{Time.time} OnFireChanged value {changed.Behaviour.isFiring}");

        bool isFiringCurrent = changed.Behaviour.isFiring;

        //Load the old value
        changed.LoadOld();

        bool isFiringOld = changed.Behaviour.isFiring;

        if (isFiringCurrent && !isFiringOld)
            changed.Behaviour.OnFireRemote();

    }

    void OnFireRemote()
    {
        if (!Object.HasInputAuthority)
            fireParticleSystemRemotePlayer.Play();
    }
}
