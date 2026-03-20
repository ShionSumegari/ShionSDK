using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public BoardView boardView;
    
    GameController controller;

    private void Start()
    {
        BoardModel board = new BoardModel(8, 8);
        controller = new GameController(board, boardView);
        controller.Initalize();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            int x = Mathf.RoundToInt(worldPos.x);
            int y = Mathf.RoundToInt(worldPos.y);
            
            controller.OnTileClicked(x, y);
        }
    }
}
