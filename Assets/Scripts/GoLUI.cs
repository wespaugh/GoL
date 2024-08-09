using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class GoLUI : MonoBehaviour
{
  [SerializeField]
  GoLBoard board;

  [SerializeField]
  TMP_InputField initialProbability = null;
  [SerializeField]
  TMP_InputField boundsX = null;
  [SerializeField]
  TMP_InputField boundsY = null;
  [SerializeField]
  TMP_InputField refreshRate = null;

  [SerializeField]
  Button startButton = null;

  [SerializeField]
  Button stopButton = null;

  private void Awake()
  {
    // by default, we can start the simulation, but not stop it until it's running
    stopButton.interactable = false;

    // validating inputs will clear out any bad values in the prefab
    validateInputs();
  }

  public void RunSimulation()
  {
    // commit possible changes to the board size to the board
    // (this will restart the simulation if needed due to board size)
    ParseInputs();
    board.StartSimulation();

    // toggle the buttons so we can stop, but not start the simulation a second time
    stopButton.interactable = true;
    startButton.interactable = false;
  }

  public void StopSimulation()
  {
    // stop the simulation
    board.StopSimulation();

    // toggle the buttons so we can start again, but not stop
    startButton.interactable = true;
    stopButton.interactable = false;
  }

  // Commit the current parameters to the board, flagging that the board is to be reset, and start a new simulation
  public void Restart()
  {
    // get current parameters, pass them to the board with a flag that forces a new board to be made
    ParseInputs(true);
    // start simulation on that new board
    board.StartSimulation();
    // allow the user to stop the simulation, but not start it a second time
    startButton.interactable = false;
    stopButton.interactable = true;
  }

  // callback to UI Widgets when their values change
  public void validateInputs()
  {
    ParseInputs();
  }
  // get UI widget values and pass them to the board
  // optionally, the updateBoard flag can be set to force a new board to be made
  private void ParseInputs(bool updateBoard = false)
  {
    float probability = GoLBoard.DEFAULT_PROBABILITY_OF_LIFE;
    int bX = GoLBoard.DEFAULT_BOARD_SIZE_X;
    int bY = GoLBoard.DEFAULT_BOARD_SIZE_Y;
    int refresh = GoLBoard.DEFAULT_REFRESH_RATE;

    try { probability = Mathf.Clamp(float.Parse(initialProbability.text), 0.0f, 1.0f); }
    catch (FormatException fe) { }
    try { bX = clampPositiveInt(boundsX); }
    catch (FormatException fe) { }
    try { bY = clampPositiveInt(boundsY); }
    catch (FormatException fe) { }
    try { refresh = clampPositiveInt(refreshRate, 1); }
    catch (FormatException fe) { }

    initialProbability.text = $"{probability}";
    boundsX.text = $"{bX}";
    boundsY.text = $"{bY}";
    refreshRate.text = $"{refresh}";

    board.updateParams(bX, bY, refresh, probability, updateBoard);
  }

  // helper function for widget value sanitization
  private int clampPositiveInt(TMP_InputField input, int min = 1)
  {
    int value = int.Parse(input.text);
    return value < min ? min : value;
  }

}
