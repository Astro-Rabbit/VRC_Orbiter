using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SimHandoffNetState : UdonSharpBehaviour
{
    [Header("Public mirrors")]
    public int txnId;
    public int targetPlayerId;
    public int sourcePlayerId;
    public byte state; // 0=IDLE, 1=READY, 2=Established

    public double simT;

    public byte mode;
    public byte primaryBodyId;

    public double rx, ry, rz;
    public double vx, vy, vz;

    public float qx, qy, qz, qw;
    public float wx, wy, wz;


    // ---------------------------------------------------------------------
    // Synced backing fields
    // ---------------------------------------------------------------------

    [UdonSynced] private int _rev;

    [UdonSynced] private int _txnId;
    [UdonSynced] private int _targetPlayerId;
    [UdonSynced] private int _sourcePlayerId;
    [UdonSynced] private byte _state;

    [UdonSynced] private double _simT;

    [UdonSynced] private byte _mode;
    [UdonSynced] private byte _primaryBodyId;

    [UdonSynced] private double _rx, _ry, _rz;
    [UdonSynced] private double _vx, _vy, _vz;

    [UdonSynced] private float _qx, _qy, _qz, _qw;
    [UdonSynced] private float _wx, _wy, _wz;



    private int _appliedRev = -1;

    public bool IsActive()
    {
        return state == 1;
    }

    public void CommitAndSerialize()
    {
        _txnId = txnId;
        _targetPlayerId = targetPlayerId;
        _sourcePlayerId = sourcePlayerId;
        _state = state;

        _simT = simT;

        _mode = mode;
        _primaryBodyId = primaryBodyId;

        _rx = rx; 
        _ry = ry; 
        _rz = rz;
        _vx = vx; 
        _vy = vy; 
        _vz = vz;


        _qx = qx; 
        _qy = qy; 
        _qz = qz; 
        _qw = qw;
        _wx = wx; 
        _wy = wy; 
        _wz = wz;


        _rev++;


        Debug.Log(
            $"[SimHandoffNetState] COMMIT " +
            $"rev={_rev} " +
            $"PUB txn={txnId} state={state} target={targetPlayerId} source={sourcePlayerId} " +
            $"SYNC txn={_txnId} state={_state} target={_targetPlayerId} source={_sourcePlayerId}"
        );


        RequestSerialization();
        _appliedRev = _rev;
    }

    private void CopySyncedToPublic()
    {
        txnId = _txnId;
        targetPlayerId = _targetPlayerId;
        sourcePlayerId = _sourcePlayerId;
        state = _state;

        simT = _simT;

        mode = _mode;
        primaryBodyId = _primaryBodyId;

        rx = _rx; ry = _ry; rz = _rz;
        vx = _vx; vy = _vy; vz = _vz;

        qx = _qx; qy = _qy; qz = _qz; qw = _qw;
        wx = _wx; wy = _wy; wz = _wz;

    }

    public override void OnPostSerialization(VRC.Udon.Common.SerializationResult result)
    {
        Debug.Log(
            $"[SimHandoffNetState] POST " +
            $"success={result.success} bytes={result.byteCount} " +
            $"rev={_rev} " +
            $"SYNC txn={_txnId} state={_state} target={_targetPlayerId} source={_sourcePlayerId}"
        );
    }

    public override void OnDeserialization()
    {
        Debug.Log(
            $"[SimHandoffNetState] DESER RAW " +
            $"rev={_rev} " +
            $"SYNC txn={_txnId} state={_state} target={_targetPlayerId} source={_sourcePlayerId}"
        );

        if (_rev == _appliedRev)
        {
            Debug.Log($"[SimHandoffNetState] DESER IGNORED rev={_rev} already applied");
            return;
        }

        _appliedRev = _rev;
        CopySyncedToPublic();

        Debug.Log(
            $"[SimHandoffNetState] DESER APPLIED " +
            $"PUB txn={txnId} state={state} target={targetPlayerId} source={sourcePlayerId}"
        );
    }
}