using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Opt-in runtime smoke test for the generated interaction playground. Creating the request file
// enters Play Mode, transfers a real fridge vial through the same Interact methods the player
// uses, drives/steers, releases into a coast, and finally verifies a wall impact stops the cart.
[InitializeOnLoad]
public static class UtilityCartPlayProbe
{
    const string RequestPath = "Temp/UtilityCartPlayProbe.request";
    const string PendingKey = "Roosevelt.UtilityCartPlayProbe.Pending";
    static int phase;
    static double phaseStarted;
    static UtilityCart cart;
    static PlayerInteraction player;
    static Carryable sample;
    static Vector3 driveStartPosition;
    static Quaternion driveStartRotation;
    static Vector3 releasePosition;
    static Vector3 impactStartPosition;
    static Vector3 impactForward;
    static GameObject blocker;

    static UtilityCartPlayProbe() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (!EditorApplication.isPlaying)
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(RequestPath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
            return;
        }

        if (!SessionState.GetBool(PendingKey, false)) return;
        try
        {
            // Error Pause may be enabled in the Console; unrelated package errors must not freeze
            // the game while the editor-side probe clock keeps advancing.
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            if (phase == 0) BeginProbe();
            else if (phase == 1) WaitForFirstDrive();
            else if (phase == 2 && Elapsed > 0.9) VerifyDriveAndRelease();
            else if (phase == 3 && Elapsed > 0.22) VerifyCoast();
            else if (phase == 4) WaitForSecondDrive();
            else if (phase == 5 && Elapsed > 0.8) VerifyImpactAndFinish();
            if (phase > 0 && Elapsed > 6.0) throw new TimeoutException($"probe phase {phase} timed out");
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    static void BeginProbe()
    {
        EditorApplication.isPaused = false;
        Time.timeScale = 1f;
        cart = UnityEngine.Object.FindAnyObjectByType<UtilityCart>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerInteraction>();
        if (cart == null || player == null) throw new InvalidOperationException("cart or player was not found");

        // The authored placement hugs the west work line. Move only the Play Mode instance into
        // the empty room centre so inertia and the deliberately spawned wall are independent tests.
        cart.transform.SetPositionAndRotation(new Vector3(-6f, 0f, -5f), Quaternion.identity);

        sample = UnityEngine.Object.FindObjectsByType<Carryable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.PartId == MachineParts.Sample && candidate.Holder != null);
        if (sample == null) throw new InvalidOperationException("no stocked fridge sample was found");

        var hands = player.GetComponent<CarryHands>();
        sample.Interact(player);
        if (hands == null || hands.Held != sample) throw new InvalidOperationException("player could not pick up the fridge sample");

        cart.Interact(player);
        var mounted = cart.GetComponentsInChildren<Socket>(true).Any(slot => slot.Mounted == sample);
        if (!mounted || hands.IsFull) throw new InvalidOperationException("cart interaction did not mount the sample in an empty socket");

        cart.Interact(player);
        if (!cart.IsTransporting) throw new InvalidOperationException("empty-hand interaction did not engage cart transport");
        phase = 1;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    static void WaitForFirstDrive()
    {
        if (!cart.IsDriving) return;
        if (!player.GetComponent<FirstPersonController>().IsCartControlled)
            throw new InvalidOperationException("player did not enter cart control mode at the handle");
        driveStartPosition = cart.transform.position;
        driveStartRotation = cart.transform.rotation;
        cart.SetEditorDriveInput(new Vector2(0.45f, 1f));
        phase = 2;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    static void VerifyDriveAndRelease()
    {
        cart.SetEditorDriveInput(Vector2.zero);
        if (Vector3.Distance(cart.transform.position, driveStartPosition) < 0.35f)
            throw new InvalidOperationException("W drive input did not move the cart forward");
        if (Quaternion.Angle(cart.transform.rotation, driveStartRotation) < 3f)
            throw new InvalidOperationException("W+D combined input did not steer the cart");
        if (!sample.transform.IsChildOf(cart.transform))
            throw new InvalidOperationException("mounted sample did not remain parented to the moving cart");

        releasePosition = cart.transform.position;
        cart.Interact(player);
        if (cart.IsTransporting) throw new InvalidOperationException("second empty-hand interaction did not release cart");
        if (!cart.IsCoasting) throw new InvalidOperationException("moving cart stopped dead instead of entering coast");
        phase = 3;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    static void VerifyCoast()
    {
        if (Vector3.Distance(cart.transform.position, releasePosition) < 0.015f)
            throw new InvalidOperationException("released cart had no residual inertia");
        if (cart.IsCoasting) return;

        cart.Interact(player);
        phase = 4;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    static void WaitForSecondDrive()
    {
        if (!cart.IsDriving) return;
        impactForward = cart.transform.forward;
        impactStartPosition = cart.transform.position;
        blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "UtilityCartProbe_Wall";
        blocker.transform.SetPositionAndRotation(cart.transform.position + impactForward * 0.68f + Vector3.up,
                                                 cart.transform.rotation);
        blocker.transform.localScale = new Vector3(2f, 2f, 0.12f);
        cart.SetEditorDriveInput(Vector2.up);
        phase = 5;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    static void VerifyImpactAndFinish()
    {
        cart.SetEditorDriveInput(Vector2.zero);
        var travelled = Vector3.Dot(cart.transform.position - impactStartPosition, impactForward);
        if (travelled > 0.48f) throw new InvalidOperationException($"cart crossed the test wall ({travelled:F2} m)");
        var source = cart.GetComponent<AudioSource>();
        if (source == null || !source.isPlaying) throw new InvalidOperationException("wall impact did not play collision audio");
        if (!sample.transform.IsChildOf(cart.transform))
            throw new InvalidOperationException("sample detached during wall impact");

        cart.Interact(player);
        Debug.Log("[UtilityCartProbe] PASS: sample loaded; handle aligned; W+D steered; release coasted; wall stopped cart with impact audio");
        Stop();
    }

    static double Elapsed => EditorApplication.timeSinceStartup - phaseStarted;

    static void Fail(string reason)
    {
        Debug.LogError("[UtilityCartProbe] FAIL: " + reason);
        Stop();
    }

    static void Stop()
    {
        if (cart != null) cart.SetEditorDriveInput(null);
        if (blocker != null) UnityEngine.Object.Destroy(blocker);
        SessionState.SetBool(PendingKey, false);
        phase = 0;
        EditorApplication.isPlaying = false;
    }
}
