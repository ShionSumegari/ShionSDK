using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FitScreenController : MonoBehaviour
{
    [SerializeField] private int WIDTH_DEFAULT;
    [SerializeField] private int HEIGHT_DEFAULT;

    [SerializeField] private CanvasScaler canvasScaler;

    private float defaultRatio;
    private float screenRatio;


    private void Update()
    {
        FitScreen();
    }

    private void FitScreen()
    {
        defaultRatio = (float)WIDTH_DEFAULT / HEIGHT_DEFAULT;
        screenRatio = (float)Screen.width / Screen.height;

        if (screenRatio >= defaultRatio)
            canvasScaler.matchWidthOrHeight = 1f;
        else
            canvasScaler.matchWidthOrHeight = 0f;
    }
}
