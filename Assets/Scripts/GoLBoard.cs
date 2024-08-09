using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GoLBoard : MonoBehaviour
{
  public static readonly float DEFAULT_PROBABILITY_OF_LIFE = 0.5f;
  public static readonly int DEFAULT_BOARD_SIZE_X = 100;
  public static readonly int DEFAULT_BOARD_SIZE_Y = 100;
  public static readonly int DEFAULT_REFRESH_RATE = 500;
  [SerializeField]
  Tilemap tilemap;

  [SerializeField]
  Tile aliveTile = null;
  [SerializeField]
  Tile deadTile = null;

  [SerializeField]
  private Vector2Int Bounds = new Vector2Int(DEFAULT_BOARD_SIZE_X, DEFAULT_BOARD_SIZE_Y);

  [Range(0.0f, 1.0f)]
  [SerializeField]
  float initialLifeProbability = .5f;

  // as we update the simulation, the 'current' iteration of the board will oscillate between A and B
  //
  // note that these are 2D arrays condensed onto a single bitmap. each (x,y) position is indexed at (y*Bounds.width + x).
  // Only access these bitmaps through their accessor functions (setBoardValue(x,y,v), getBoardValue(x,y), etc.) to guarantee proper indexing
  bool[] boardA = null;
  bool[] boardB = null;

  // the flag that determines which board to look at. flips once per simulation step
  bool useBoardA = true;

  bool firstRun = true;
  bool running = false;
  bool stopRequested = false;

  // a flag to allow click-and-drag to invert tile statuses
  bool holdingClick = false;

  // the refresh period of the simulation
  private float refreshMilliseconds = 500.0f;

  // the most recently clicked tile, to prevent click-and-drag from repeatedly toggling the same tile
  Vector3 mouseClickCache = Vector3.negativeInfinity;

  private void Awake()
  {
    // create a default board so we're ready to start
    firstRun = true;
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


      var tileOffset = tilemap.WorldToCell(worldPoint);

      // only allow a tile to be toggled once until the mouse moves off of it
      if (tileOffset == mouseClickCache)
      {
        return;
      }
      mouseClickCache = tileOffset;

      // Try to get a tile from cell position
      var tile = tilemap.GetTile(tileOffset);

      // if we did click on a tile
      if (tile)
      {
        // to center the grid on the screen, we use a negative index on the tilemap, so we need to add that offset back to get values that can be array-indexed against the
        // alive/dead data
        Vector3Int arrayPosition = new Vector3Int(tileOffset.x + Bounds.x / 2, tileOffset.y + Bounds.y / 2, 0);
        bool newValue = !getBoardValue(arrayPosition.x, arrayPosition.y);
        setBoardValue(arrayPosition.x, arrayPosition.y, newValue);
        tilemap.SetTile(tileOffset, newValue ? aliveTile : deadTile);

      }
    }
  }

  /**
   * Update Simulation parameters from the UI.
   * Changes to the board will require a restart before taking effect. Refresh will be updated at the next simulation tick
   */
  public void updateParams(int x, int y, int refresh, float initProbability, bool forceRestart = false)
  {
    // the board will need to be remade if that's specifically requested, or if the size of the board changed
    bool dirty = Bounds.x != x || Bounds.y != y || forceRestart;

    // cache the new simulation parameters
    Bounds = new Vector2Int(x, y);
    initialLifeProbability = initProbability;
    refreshMilliseconds = refresh;

    // if we need to restart the simulation, do so
    if (dirty)
    {
      tilemap.ClearAllTiles();
      initBoard();
    }
  }

  // Initialize a board when the application starts or when the bounds of the board are changed
  private void initBoard()
  {
    // create both of the frame buffers (A will be the first 'current' step, and B will be the first 'next' step, and they will switch each tick)
    boardA = new bool[Bounds.x * Bounds.y];
    boardB = new bool[Bounds.x * Bounds.y];

    // initialize both values to some random value based on the probability of life specified
    for (int i = 0; i < boardA.Length; i++)
    {
      boardA[i] = UnityEngine.Random.Range(0.01f, 1.0f) <= initialLifeProbability;
      boardB[i] = boardA[i];
    }
    useBoardA = true;

    // update the tilemap dimensions
    tilemap.size = new Vector3Int(Bounds.x, Bounds.y);

    // setting this flag makes the simulation show the first frame for a refresh period before starting the tickloop
    firstRun = true;
    renderBoard();
  }

  // Set a value on the 'before' board, in a simulation tick before/after
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

  // set a value on the 'after' board, in a simulation tick before/after
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

  // get a value from the 'before' board, in a simulation step before / after
  private bool getBoardValue(int x, int y)
  {
    return useBoardA ? boardA[y*Bounds.x + x] : boardB[y * Bounds.x + x];
  }

  // the first loop of a simulation, we want to wait
  // a full tick before the main simulation loop kicks off.
  private IEnumerator showFirstFrameThenTick()
  {
    yield return new WaitForSeconds(refreshMilliseconds / 1000.0f);
    StartCoroutine(tick());
  }

  // begins the simulation and immediately executes a step and renders a frame
  // then, the loop waits a Refresh amount of time and starts the loop again
  private IEnumerator tick()
  {
    stepSimulation();
    renderBoard();

    yield return new WaitForSeconds(refreshMilliseconds / 1000.0f);

    if (stopRequested)
    {
      stopRequested = false;
      running = false;
      yield break;
    }

    StartCoroutine(tick());
  }

  // updates all of the tiles to their current alive/dead state based on the current simulation representation
  private void renderBoard()
  {
    for (int i = 0; i < Bounds.x; i++)
    {
      for (int j = 0; j < Bounds.y; j++)
      {
        // offset by half the board size to keep it centered in the view
        Vector3Int startLoc = new Vector3Int((-Bounds.x / 2) + i, (-Bounds.y / 2) + j);
        tilemap.SetTile(startLoc, getBoardValue(i, j) ? aliveTile : deadTile);
      }
    }
  }

  private void stepSimulation()
  {
    // loop over every existing cell
    for (int i = 0; i < Bounds.x; ++i)
    {
      for( int j = 0;j < Bounds.y; ++j)
      {
        bool currentStatus = getBoardValue(i, j);

        int livingNeightbors = getNeighborsAtPosition(i,j);
        // if cell was dead previously, cell comes back alive if exactly 3 neighbors lived
        if (!currentStatus)
        {
          setUpdatedBoardValue(i, j, livingNeightbors == 3);
        }
        // if cell was alive previously, cell stays alive if it has 2 or 3 neighbors
        else
        {
          setUpdatedBoardValue(i, j, livingNeightbors == 2 || livingNeightbors == 3);
        }
      }
    }
    useBoardA = !useBoardA;
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
        // not the biggest priority as it is an edge case of an edge case of an edge case, but worth a mention

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

  // sets a flag that will stop the simulation from taking another step once the current tick() finishes
  public void StopSimulation()
  {
    stopRequested = true;
  }

  // starts or restarts the simulation. firstRun will be set if the board has just been created,
  // in which case the initial frame is shown for one refresh period before the simulation starts updating
  public void StartSimulation()
  {
    // if we ninja-clicked stop then start, just clear the stop request so the simulation continues unabated
    if(stopRequested)
    {
      stopRequested = false;
    }
    // if we are already running (shouldn't be possible since the button is disabled, but just in case), bail out rather than
    // start multiple concurrent tick loops
    if(running) return;

    // set that flag so duplicate ticks don't start
    running = true;

    // if the simulation is just starting, show the default frame before beginning the tick
    if (firstRun)
    {
      firstRun = false;
      StartCoroutine(showFirstFrameThenTick());
    }
    // if we've paused and restarted the simulation, start the main tick loop, which will immediately step the simulation forward
    // before waiting another refresh period
    else
    {
      StartCoroutine(tick());
    }
  }
}
