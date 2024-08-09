using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GoLBoard : MonoBehaviour
{
  [SerializeField]
  Tilemap t;

  [SerializeField]
  Tile aliveTile = null;
  [SerializeField]
  Tile deadTile = null;

  [SerializeField]
  private Vector2Int Bounds = new Vector2Int(100, 100);

  [Range(0.0f, 1.0f)]
  [SerializeField]
  float initialLifeProbability = .5f;

  // as we update the simulation, the 'current' iteration of the board will oscillate between A and B
  bool[] boardA = null;
  bool[] boardB = null;

  // the flag that determines which board to look at. flips once per simulation step
  bool useBoardA = true;

  // as an efficiency solution, keep a bitmap of things that have changed
  // algorithm will iterate over One Map to see what might need updated,
  // and as it finds those locations change it will flip a bit and all its neighbords to 1 on The Other map
  // we'll use the same useBoardA flag to flip between them each simulation step
  bool[] changeMapA = null;
  bool[] changeMapB = null;

  bool firstRun = true;
  bool running = false;
  bool stopRequested = false;

  bool holdingClick = false;

  private float refreshMilliseconds = 500.0f;

  Vector3 mouseClickCache = Vector3.negativeInfinity;

  private void Awake()
  {
    initBoard();
  }

  public void Update()
  {
    if(Input.GetMouseButtonUp(0))
    {
      holdingClick = false;
      mouseClickCache = Vector3.negativeInfinity;
    }
    else if (Input.GetMouseButtonDown(0) || holdingClick)
    {
      holdingClick = true;
      Vector3 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);


      var tileOffset = t.WorldToCell(worldPoint);

      if (tileOffset == mouseClickCache)
      {
        return;
      }
      mouseClickCache = tileOffset;

      // Try to get a tile from cell position
      var tile = t.GetTile(tileOffset);

      // if we did click on a tile
      if (tile)
      {
        // to center the grid on the screen, we use a negative index on the tilemap, so we need to add that offset back to get values that can be array-indexed against the
        // alive/dead data
        Vector3Int arrayPosition = new Vector3Int(tileOffset.x + Bounds.x / 2, tileOffset.y + Bounds.y / 2, 0);
        bool newValue = !getBoardValue(arrayPosition.x, arrayPosition.y);
        setBoardValue(arrayPosition.x, arrayPosition.y, newValue);
        t.SetTile(tileOffset, newValue ? aliveTile : deadTile);

      }
    }
  }

  public void updateParams(int x, int y, int refresh, float initProbability, bool forceRestart = false)
  {
    bool dirty = Bounds.x != x || Bounds.y != y || forceRestart;
    Bounds = new Vector2Int(x, y);
    initialLifeProbability = initProbability;
    refreshMilliseconds = refresh;

    if (dirty)
    {
      t.ClearAllTiles();
      initBoard();
    }
  }

  private void initBoard()
  {
    boardA = new bool[Bounds.x * Bounds.y];
    boardB = new bool[Bounds.x * Bounds.y];
    changeMapA = new bool[Bounds.x * Bounds.y];
    changeMapB = new bool[Bounds.x * Bounds.y];

    for (int i = 0; i < boardA.Length; i++)
    {
      boardA[i] = UnityEngine.Random.Range(0.01f, 1.0f) <= initialLifeProbability;
      boardB[i] = boardA[i];
      // inspect everything on the first pass
      changeMapA[i] = true;
      changeMapB[i] = false;
    }
    useBoardA = true;
  }

  private void setBoardValue(int x, int y, bool value)
  {
    if(useBoardA)
    {
        boardA[y * Bounds.x + x] = value;
    }
    else
    {
      boardB[y * Bounds.x + x] = value;
    }
  }

  private void setUpdatedBoardValue(int x, int y, bool value)
  {
    if(useBoardA)
    {
      boardB[y * Bounds.x + x] = value;
    }
    else
    {
      boardA[y * Bounds.x + x] = value;
    }
  }

  private bool getBoardValue(int x, int y)
  {
    return useBoardA ? boardA[y*Bounds.x + x] : boardB[y * Bounds.x + x];
  }

  private bool getCellChanged(int x, int y)
  {
    return useBoardA ? changeMapA[y * Bounds.x + x] : changeMapB[y * Bounds.x + x];
  }
  private void setCellChanged(int x, int y)
  {
    // note that if useBoardA is true, we update changeMapB and vice versa.
    // this is because useBoardA denotes the board we are iterating over, so we need to be updating the other one
    bool[] changeMap = useBoardA ? changeMapB : changeMapA;

    // flip all the neighbors, as well
    for (int i = -1; i <= 1; ++i)
    {
      for (int j = -1; j <= 1; ++j)
      {
        if (x + i < 0 || x + i >= Bounds.x) continue;
        if(y + j < 0 || y + j >= Bounds.y) continue;
        changeMap[y * Bounds.x + x] = true;
      }
    }
  }

  private void resetChangeMap()
  {
    bool[] changeMap = useBoardA ? changeMapA : changeMapB;
    for (int i = 0; i < changeMap.Length; ++i) { changeMap[i] = false; }
  }

  private IEnumerator tick()
  {
    firstRun = false;
    t.size = new Vector3Int(Bounds.x, Bounds.y);
    for (int i = 0; i < Bounds.x; i++)
    {
      for (int j = 0; j < Bounds.y; j++)
      {
        // offset by half the board size to keep it centered in the view
        Vector3Int startLoc = new Vector3Int((-Bounds.x/2) + i, (-Bounds.y/2) + j);
        t.SetTile(startLoc, getBoardValue(i,j) ? aliveTile : deadTile);
      }
    }


    yield return new WaitForSeconds(refreshMilliseconds / 1000.0f);

    if (stopRequested)
    {
      stopRequested = false;
      running = false;
      yield break;
    }

    stepSimulation();
    StartCoroutine(tick());
  }

  private void stepSimulation()
  {
    // int complexity = 0;
    // loop over every existing cell
    for (int i = 0; i < Bounds.x; ++i)
    {
      for( int j = 0;j < Bounds.y; ++j)
      {
        if (!getCellChanged(i, j)) continue;
        bool currentStatus = getBoardValue(i, j);

        int livingNeightbors = getNeighborsAtPosition(i,j);
        // if cell was dead previously, cell comes back alive if exactly 3 neighbors lived
        if (!currentStatus)
        {
          if (livingNeightbors == 3)
          {
            setUpdatedBoardValue(i, j, true);
            setCellChanged(i, j);
          }
          else
          {
            setUpdatedBoardValue(i, j, false);
          }
        }
        // if cell was alive previously, cell stays alive if it has 2 or 3 neighbors
        else
        {
          if (!(livingNeightbors == 2 || livingNeightbors == 3))
          {
            setUpdatedBoardValue(i, j, false);
            setCellChanged(i, j);
          }
          else
          {
            setUpdatedBoardValue(i, j, true);
          }
        }
      }
    }
    // resetChangeMap();
    useBoardA = !useBoardA;
    // Debug.Log("Complexity: " + complexity);
  }

  private int getNeighborsAtPosition(int i, int j)
  {
    int livingNeighbors = 0;
    int neighborX, neighborY;

    /**********
     * Consider the neighbors in this order
     * 
     * 123
     * 4X5
     * 678
     * 
     **********/

    for (int ni = -1; ni <= 1; ++ni)
    {
      neighborX = i + ni;

      // if neighbor isn't on the map, don't consider it
      if (neighborX < 0 || neighborX >= Bounds.x) continue;

      for (int nj = -1; nj <= 1; ++nj)
      {
        neighborY = j + nj;

        // small optimization thought: if cell is dead (pass that into the function) and there aren't enough remaining neighbors to revitalize it, break early

        // if neighbor isn't on the map, continue;
        if (neighborY < 0 || neighborY >= Bounds.y) continue;

        // if we're considering the cell, rather than a neighbor, continue
        if (ni == 0 && nj == 0) continue;

        // if the neighbor there is alive, count it
        if (getBoardValue(neighborX, neighborY))
        {
          ++livingNeighbors;
        }

        // we never care how many neighbors live beyond 3, so stop counting here
        if (livingNeighbors > 3) break;
      }
      // we never care how many neighbors live beyond 3, so stop counting here
      if (livingNeighbors > 3) break;
    }
    return livingNeighbors;
  }

  public void StopSimulation()
  {
    stopRequested = true;
  }

  public void StartSimulation()
  {
    if(running) return;
    if(firstRun)
    {
      initBoard();
    }
    running = true;
    StartCoroutine(tick());
  }
}
