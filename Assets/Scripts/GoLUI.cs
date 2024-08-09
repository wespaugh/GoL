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

  [SerializeField]
  Button restartButton = null;

  private void Awake()
  {
    stopButton.interactable = false;
    validateInputs();
  }

  public void RunSimulation()
  {
    ParseInputs();
    board.StartSimulation();
    stopButton.interactable = true;
    startButton.interactable = false;
  }

  public void StopSimulation()
  {
    board.StopSimulation();
    startButton.interactable = true;
    stopButton.interactable = false;
  }

  public void Restart()
  {
    ParseInputs(true);
    board.StartSimulation();
    startButton.interactable = false;
    stopButton.interactable = true;
  }

  public void validateInputs()
  {
    ParseInputs();
  }
  private void ParseInputs(bool updateBoard = false)
  {
    float probability = .5f;
    int bX = 100;
    int bY = 100;
    int refresh = 500;

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

  private int clampPositiveInt(TMP_InputField input, int min = 1)
  {
    int value = int.Parse(input.text);
    return value < min ? min : value;
  }

}
