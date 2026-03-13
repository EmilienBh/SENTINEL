/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class HandedInputSelector : MonoBehaviour {
    OVRCameraRig m_CameraRig;
    OVRInputModule m_InputModule;

    enum ActiveController { None, Left, Right };
    ActiveController activeController = ActiveController.None;

    void Start() {
        m_CameraRig = FindObjectOfType<OVRCameraRig>();
        m_InputModule = FindObjectOfType<OVRInputModule>();

        if (OVRInput.GetActiveController() == OVRInput.Controller.LTouch) {
            SetActiveController(OVRInput.Controller.LTouch);
        }
        else {
            SetActiveController(OVRInput.Controller.RTouch);
        }
    }

    void Update() {
        /*
         * Update main controller on click (so pointer sticks to that hand)
         */
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)) {
            SetActiveController(OVRInput.Controller.LTouch);
        }
        else if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch)) {
            SetActiveController(OVRInput.Controller.RTouch);
        }
    }

    void SetActiveController(OVRInput.Controller c) {
        Transform t;
        if (c == OVRInput.Controller.LTouch) {
            t = m_CameraRig.leftHandAnchor;
        }
        else {
            t = m_CameraRig.rightHandAnchor;
        }

        m_InputModule.rayTransform = t;
    }
}
