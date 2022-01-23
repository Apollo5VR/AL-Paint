using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Normal.Realtime;

public class Brush : MonoBehaviour
{
    // Reference to Realtime to use to instantiate brush strokes
    [SerializeField] private Realtime _realtime;

    // Prefab to instantiate when we draw a new brush stroke
    [SerializeField] private GameObject _brushStrokePrefab = null;

    [SerializeField] private Transform _cubeTransform = null;

    // Which hand should this brush instance track?
    private enum Hand { LeftHand, RightHand };
    [SerializeField] private Hand _hand = Hand.RightHand;

    // Used to keep track of the current brush tip position and the actively drawing brush stroke
    private Vector3 _handPosition;
    private Quaternion _handRotation;
    private BrushStroke _activeBrushStroke;
    private UnityEngine.XR.InputDevice handDeviceL;
    private UnityEngine.XR.InputDevice handDeviceR;
    private UnityEngine.XR.InputDevice selectedHandDevice;

    //GG
    private void Start()
    {
        var inputDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevices(inputDevices);

        foreach (var device in inputDevices)
        {
            Debug.Log(string.Format("Device found with name '{0}' and role '{1}'", device.name, device.role.ToString()));
        }

        AssignHandXR();
    }

    private void AssignHandXR()
    {
        var leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);

        if (leftHandDevices.Count == 1)
        {
            handDeviceL = leftHandDevices[0];
            Debug.Log(string.Format("Device name '{0}' with role '{1}'", handDeviceL.name, handDeviceL.role.ToString()));
        }
        else if (leftHandDevices.Count > 1)
        {
            Debug.Log("Found more than one left hand!");
        }

        var rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);

        if (rightHandDevices.Count == 1)
        {
            handDeviceR = rightHandDevices[0];
            Debug.Log(string.Format("Device name '{0}' with role '{1}'", handDeviceR.name, handDeviceR.role.ToString()));
        }
        else if (rightHandDevices.Count > 1)
        {
            Debug.Log("Found more than one right hand!");
        }

        if (_hand == Hand.RightHand)
        {
            selectedHandDevice = handDeviceR;
        }
        else
        {
            selectedHandDevice = handDeviceL;
        }
    }

    private void Update()
    {
        if (!_realtime.connected)
            return;

        if(selectedHandDevice.characteristics == InputDeviceCharacteristics.None)
        {
            AssignHandXR();
        }

        // Start by figuring out which hand we're tracking
        XRNode node = _hand == Hand.LeftHand ? XRNode.LeftHand : XRNode.RightHand;
        //string trigger = _hand == Hand.LeftHand ? "Left Trigger" : "Right Trigger";

        // Get the position & rotation of the hand
        bool handIsTracking = UpdatePose(node, ref _handPosition, ref _handRotation);

        bool triggerPressed;
        bool triggerValue;
        if (selectedHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out triggerValue) && triggerValue)
        {
            triggerPressed = true;
            //Debug.Log("Trigger button is pressed.");
        }
        else
        {
            triggerPressed = false;
        }

        //GG testing wall
        _handRotation = _cubeTransform.rotation;
        //GG for some reason we need "-1.25" when multiplayer is connected to accurately align with the VR player hand height
        _handPosition = new Vector3(_handPosition.x, _handPosition.y - 1.25f, _cubeTransform.position.z - Random.Range(0.01f, 0.02f)); //to offset it in front of wall, random to avoid z texture clipping


        //Debug.Log("L Hand position y is: " + _handPosition.y);

        // If we lose tracking, stop drawing
        if (!handIsTracking)
            triggerPressed = false;

        // If the trigger is pressed and we haven't created a new brush stroke to draw, create one!
        if (triggerPressed && _activeBrushStroke == null)
        {
            // Instantiate a copy of the Brush Stroke prefab.
            GameObject brushStrokeGameObject = Realtime.Instantiate(_brushStrokePrefab.name, Realtime.InstantiateOptions.defaults); //these are defaultss - ownedByClient: true, useInstance: _realtime

            // Grab the BrushStroke component from it
            _activeBrushStroke = brushStrokeGameObject.GetComponent<BrushStroke>();

            // Tell the BrushStroke to begin drawing at the current brush position
            _activeBrushStroke.BeginBrushStrokeWithBrushTipPoint(_handPosition, _handRotation);
        }

        // If the trigger is pressed, and we have a brush stroke, move the brush stroke to the new brush tip position
        if (triggerPressed)
            _activeBrushStroke.MoveBrushTipToPoint(_handPosition, _handRotation);

        // If the trigger is no longer pressed, and we still have an active brush stroke, mark it as finished and clear it.
        if (!triggerPressed && _activeBrushStroke != null)
        {
            _activeBrushStroke.EndBrushStrokeWithBrushTipPoint(_handPosition, _handRotation);
            _activeBrushStroke = null;
        }
    }

    //// Utility

    // Given an XRNode, get the current position & rotation. If it's not tracking, don't modify the position & rotation.
    private static bool UpdatePose(XRNode node, ref Vector3 position, ref Quaternion rotation)
    {
        List<XRNodeState> nodeStates = new List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        foreach (XRNodeState nodeState in nodeStates)
        {
            if (nodeState.nodeType == node)
            {
                Vector3 nodePosition;
                Quaternion nodeRotation;
                bool gotPosition = nodeState.TryGetPosition(out nodePosition);
                bool gotRotation = nodeState.TryGetRotation(out nodeRotation);

                if (gotPosition)
                    position = nodePosition;
                if (gotRotation)
                    rotation = nodeRotation;

                return gotPosition;
            }
        }

        return false;
    }
}
