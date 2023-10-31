using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using ThreeDMaze;
using UnityEngine;
using Random = UnityEngine.Random;

public class NotThreeDMazeModule : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio KMAudio;
    public KMSelectable[] Buttons;
    public KMRuleSeedable RuleSeedable;

    public MeshRenderer MR_ble;
    public MeshRenderer MR_blw;
    public MeshRenderer MR_bre;
    public MeshRenderer MR_brw;
    public MeshRenderer MR_bw;
    public MeshRenderer MR_fle;
    public MeshRenderer MR_flw;
    public MeshRenderer MR_fre;
    public MeshRenderer MR_frw;
    public MeshRenderer MR_fw;

    public MeshRenderer MR_letter;
    public MeshRenderer MR_letter_far;

    public Material MatA;
    public Material MatB;
    public Material MatC;
    public Material MatD;
    public Material MatH;
    public Material MatE;
    public Material MatN;
    public Material MatS;
    public Material MatW;

    public Renderer[] AllMaterials;

    private const int CodeLeft = 0;
    private const int CodeRight = 2;
    private const int CodeStraight = 1;

    private bool isActive = false;
    private int moduleId;
    private static int moduleIdCounter = 1;


    private class Walls
    {
        public bool W, N;
        public char Label;
        public Walls()
        {
            W = true;
            N = true;
            Label = (char)0;
        }

        public Walls(Walls copy)
        {
            W = copy.W;
            N = copy.N;
            Label = copy.Label;
        }

        public static bool operator ==(Walls a, Walls b) => a.W == b.W && a.N == b.N;
        public static bool operator !=(Walls a, Walls b) => !(a == b);
    }
    private Walls[][] _maze;
    private Coordinate _playerA, _playerB;
    private int _rotA, _rotB;
    private bool _playerDisplayed, _isSolved, _holding, _greened;
    private float _holdStart;

    protected void Start()
    {
        // In Unity, we assigned all 8 possible letter materials to the MR_letter game object because otherwise they do not show up on Mac.
        // Here we reduce the number of materials back to one.
        MR_letter.materials = new[] { MatA };

        moduleId = moduleIdCounter++;

        UpdateDisplay(new MapView());

        BombModule.OnActivate += OnActivate;

        for (int i = 0; i < 3; i++)
        {
            int j = i;
            Buttons[j].OnInteract += delegate () { HandlePress(j, true); return false; };
            Buttons[j].OnInteractEnded += delegate () { HandlePress(j, false); };
        }

        HandleRuleseed();
        // Under extraordinarily unlikely circumstances this could fail, resulting in an exception.
        // However, this would require a particularly bad ruleseed and a particularly bad generation.
        // Additionally, I believe it's not actually possibly for this to happen, although I cannot prove that.
        // Just to be safe, if this fails, the module will autosolve.
        try
        {
            Disambiguate();
        }
        catch
        {
            Solve(error: true);
        }
        ChooseSpawns();

        Log("The maze:\n[Not 3D Maze #" + moduleId + "] " + Enumerable.Range(0, 33).Select(y =>
              Enumerable.Range(0, 67).Select(x =>
              {
                  var xWall = (x - 2) % 4 == 0;
                  var yWall = y % 2 == 0;
                  var xx = (x - 2) / 4;
                  var yy = y / 2;

                  if (x == 0)
                      return ' ';
                  else if (x == 1)
                      return ' ';
                  else if (xWall && yWall)
                  {
                      var upWall = _maze[xx % 16][(yy + 16 - 1) % 16].W;
                      var downWall = _maze[xx % 16][yy % 16].W;
                      var leftWall = _maze[(xx + 16 - 1) % 16][yy % 16].N;
                      var rightWall = _maze[xx % 16][yy % 16].N;
                      return " ╨╥║╡╝╗╣╞╚╔╠═╩╦╬"[(upWall ? 1 : 0) + (downWall ? 2 : 0) + (leftWall ? 4 : 0) + (rightWall ? 8 : 0)];
                  }
                  else if (xWall)
                  {
                      return _maze[xx % 16][yy % 16].W ? '║' : ' ';
                  }
                  else if (yWall)
                  {
                      return _maze[xx % 16][yy % 16].N ? '═' : ' ';
                  }
                  else
                      return (x - 2) % 4 == 2 && (y) % 2 == 1
                      ? (_maze[xx][yy].Label == (char)0 ? ' ' : _maze[xx][yy].Label)
                      : xx == _playerA.X && yy == _playerA.Y
                      ? "▲►▼◄"[_rotA]
                      : xx == _playerB.X && yy == _playerB.Y
                      ? "▲►▼◄"[_rotB]
                      : ' ';
              }).Join("")).Join("\n[Not 3D Maze #" + moduleId + "] "));
    }

    private void ChooseSpawns()
    {

        int a = Random.Range(0, 64);
        _playerA = new Coordinate(a % 16, a / 16);
        _rotA = Random.Range(0, 4);
        _rotB = (_rotA + 1) % 4;
        var bpos = Move(_playerA, _rotA);
        var view = new MapView()
        {
            fw = Wall(_playerA, _rotA),
            fle = Wall(_playerA, _rotA + 3),
            fre = Wall(_playerA, _rotA + 1),
            flw = Wall(Move(_playerA, _rotA + 3), _rotA),
            frw = Wall(Move(_playerA, _rotA + 1), _rotA),
            bw = Wall(bpos, _rotA),
            ble = Wall(bpos, _rotA + 3),
            bre = Wall(bpos, _rotA + 1),
            blw = Wall(Move(bpos, _rotA + 3), _rotA),
            brw = Wall(Move(bpos, _rotA + 1), _rotA),
            letter = _maze[_playerA.X][_playerA.Y].Label,
            letter_far = _maze[bpos.X][bpos.Y].Label != (char)0
        };
        Func<int, int, bool> badView = (x, y) =>
        {
            var playerB = new Coordinate(x, y);
            var bpos2 = Move(playerB, _rotB);
            var view2 = new MapView()
            {
                fw = Wall(playerB, _rotB),
                fle = Wall(playerB, _rotB + 3),
                fre = Wall(playerB, _rotB + 1),
                flw = Wall(Move(playerB, _rotB + 3), _rotB),
                frw = Wall(Move(playerB, _rotB + 1), _rotB),
                bw = Wall(bpos2, _rotB),
                ble = Wall(bpos2, _rotB + 3),
                bre = Wall(bpos2, _rotB + 1),
                blw = Wall(Move(bpos2, _rotB + 3), _rotB),
                brw = Wall(Move(bpos2, _rotB + 1), _rotB),
                letter = _maze[playerB.X][playerB.Y].Label,
                letter_far = _maze[bpos2.X][bpos2.Y].Label != (char)0
            };

            return view2.VisiblyIdenticalTo(view);
        };
        var badX = new int[] { _playerA.X, (_playerA.X + 1) % 16, (_playerA.X + 2) % 16, (_playerA.X + 14) % 16, (_playerA.X + 15) % 16 };
        var badY = new int[] { _playerA.Y, (_playerA.Y + 1) % 16, (_playerA.Y + 2) % 16, (_playerA.Y + 14) % 16, (_playerA.Y + 15) % 16 };
        _playerB = Enumerable.Range(0, 16)
            .SelectMany(x => Enumerable.Range(0, 16).Where(y => !(badX.Contains(x) && badY.Contains(y)) && !badView(x, y)).Select(y => new Coordinate(x, y)))
            .PickRandom();
        _playerDisplayed = Random.Range(0, 2) == 0;
    }

    private void Disambiguate()
    {
        Walls[][][] rotations = new Walls[][][]
        {
            Enumerable.Range(0, 16).Select(x => Enumerable.Range(0, 16).Select(y => _maze[x][y]).ToArray()).ToArray(),
            Enumerable.Range(0, 16).Select(x => Enumerable.Range(0, 16).Select(y => _maze[15 - y][x]).ToArray()).ToArray(),
            Enumerable.Range(0, 16).Select(x => Enumerable.Range(0, 16).Select(y => _maze[15 - x][15 - y]).ToArray()).ToArray(),
            Enumerable.Range(0, 16).Select(x => Enumerable.Range(0, 16).Select(y => _maze[y][15 - x]).ToArray()).ToArray()
        };

        char[] preferredLetters = "ABCDH".OrderBy(_ => Random.value).Take(3).ToArray();
        var letters = Enumerable.Repeat(0, 20)
            .SelectMany(_ => preferredLetters)
            .Concat(Enumerable.Repeat(0, 36).Select(_ => "NESW".PickRandom()))
            .OrderBy(_ => Random.value)
            .GetEnumerator();

        bool enoughLetters = false;
        letters.MoveNext();

        for (int rotId = 0; rotId < 4; rotId++)
        {
            for (int xOffset = 0; xOffset < 16; xOffset++)
            {
                for (int yOffset = 0; yOffset < 16; yOffset++)
                {
                    if (rotId == 0 && xOffset == 0 && yOffset == 0)
                        continue;

                    for (int x = 0; x < 16; x++)
                        for (int y = 0; y < 16; y++)
                            if (rotations[rotId][(x + xOffset) % 16][(y + yOffset) % 16] != _maze[x][y])
                                goto majorContinue;

                    int baseX = Random.Range(0, 4);
                    int baseY = Random.Range(0, 4);
                    for (int addX = baseX; addX < 16; addX += 4)
                    {
                        for (int addY = baseY; addY < 16; addY += 4)
                        {
                            for (int x = 0; x < 4; x++)
                                for (int y = 0; y < 4; y++)
                                    if (rotations[rotId][(x + addX) % 16][(y + addY) % 16].Label != _maze[x][y].Label)
                                        goto minorContinue;

                            var toLabel = Enumerable.Range(0, 32)
                                .Select(i => i < 16
                                    ? _maze[((i / 4) + addX) % 16][((i % 4) + addY) % 16]
                                    : rotations[rotId][(((i - 16) / 4) + addX) % 16][(((i - 16) % 4) + addY) % 16])
                                .Where(c => c.Label == (char)0)
                                .PickRandom();

                            if (!enoughLetters)
                                toLabel.Label = letters.Current;
                            else
                                toLabel.Label = preferredLetters.Concat("NESW").PickRandom();
                            if (!letters.MoveNext())
                                enoughLetters = true;
                            minorContinue:;
                        }
                    }
                majorContinue:;
                }
            }
        }

        if (enoughLetters)
            return;
        List<Walls> emptyCells = _maze.SelectMany(r => r).Where(c => c.Label == (char)0).ToList();
        do
        {
            var ix = Random.Range(0, emptyCells.Count);
            emptyCells[ix].Label = letters.Current;
            emptyCells.RemoveAt(ix);
        }
        while (letters.MoveNext());
    }

    private void Log(object s, params object[] args)
    {
        Log(s.ToString(), args);
    }
    private void Log(string s, params object[] args)
    {
        Debug.LogFormat("[Not 3D Maze #" + moduleId + "] " + s, args);
    }

    private void HandleRuleseed()
    {
        var rnd = GetComponent<KMRuleSeedable>().GetRNG();
        Log("Using ruleseed {0}.", rnd.Seed);

        const int width = 8;
        Func<Coordinate, int, Coordinate> neighbor = (cell, dir) =>
        {
            switch (dir)
            {
                case 0:
                    return new Coordinate(cell.X, (cell.Y + 7) % width);
                case 1:
                    return new Coordinate((cell.X + 1) % width, cell.Y);
                case 2:
                    return new Coordinate(cell.X, (cell.Y + 1) % width);
                default:
                    return new Coordinate((cell.X + 7) % width, cell.Y);
            }
        };

        var maze = Enumerable.Repeat(0, width).Select(_ => Enumerable.Repeat(0, width).Select(__ => new Walls()).ToArray()).ToArray();
        var todo = Enumerable.Range(0, width * width).Select(i => new Coordinate(i % width, i / width)).ToList();
        var visited = new List<Coordinate>();
        var done = new List<Coordinate>();

        var startIx = rnd.Next(0, todo.Count);
        visited.Add(todo[startIx]);
        todo.RemoveAt(startIx);

        while (todo.Count > 0)
        {
            var cellIx = rnd.Next(0, visited.Count);
            var cell = visited[cellIx];

            var validWalls = new int[] { 0, 1, 2, 3 }
                .Select(dir => new
                {
                    dir,
                    cell = neighbor(cell, dir),
                })
                .Where(c => todo.Any(t => c.cell == t))
                .ToArray();

            if (validWalls.Length == 0)
            {
                visited.RemoveAt(cellIx);
                done.Add(cell);
                continue;
            }

            var wallIx = rnd.Next(0, validWalls.Length);
            var wall = validWalls[wallIx];
            switch (wall.dir)
            {
                case 0:
                    maze[cell.X][cell.Y].N = false;
                    break;
                case 3:
                    maze[cell.X][cell.Y].W = false;
                    break;
                case 2:
                    maze[wall.cell.X][wall.cell.Y].N = false;
                    break;
                default:
                    maze[wall.cell.X][wall.cell.Y].W = false;
                    break;
            }
            todo.RemoveAll(t => t == wall.cell);
            visited.Add(wall.cell);
        }

        const float percentage = 0.65f;
        var remainingWalls = new List<int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < width; y++)
            {
                if (maze[x][y].N)
                    remainingWalls.Add((width * y + x) * 2);
                if (maze[x][y].W)
                    remainingWalls.Add((width * y + x) * 2 + 1);
            }
        }

        var removeWalls = Mathf.FloorToInt(remainingWalls.Count * percentage);

        while (removeWalls > 0)
        {
            var wallIx = rnd.Next(0, remainingWalls.Count);
            var wall = remainingWalls[wallIx];
            remainingWalls.RemoveAt(wallIx);
            var x = (wall >> 1) % width;
            var y = (wall >> 1) / width;

            if (wall % 2 == 1) maze[x][y].W = false;
            else maze[x][y].N = false;
            removeWalls--;
        }

        _maze = Enumerable.Range(0, 16).Select(x => Enumerable.Range(0, 16).Select(y => new Walls(maze[x % width][y % width])).ToArray()).ToArray();
    }

    protected void OnActivate()
    {
        isActive = true;
        StartCoroutine(Cycle());
    }

    private IEnumerator Cycle()
    {
        float delay = Random.Range(1f, 2f);

        // Start at a random point in the cycle
        _playerDisplayed = !_playerDisplayed;
        DisplayPlayer();
        yield return new WaitForSeconds(Random.Range(0f, delay));

        while (!_isSolved)
        {
            _playerDisplayed = !_playerDisplayed;
            DisplayPlayer();
            yield return new WaitForSeconds(delay);
        }
    }

    private void DisplayPlayer()
    {
        var pos = _playerDisplayed ? _playerA : _playerB;
        var rot = _playerDisplayed ? _rotA : _rotB;
        var bpos = Move(pos, rot);
        UpdateDisplay(new MapView()
        {
            fw = Wall(pos, rot),
            fle = Wall(pos, rot + 3),
            fre = Wall(pos, rot + 1),
            flw = Wall(Move(pos, rot + 3), rot),
            frw = Wall(Move(pos, rot + 1), rot),
            bw = Wall(bpos, rot),
            ble = Wall(bpos, rot + 3),
            bre = Wall(bpos, rot + 1),
            blw = Wall(Move(bpos, rot + 3), rot),
            brw = Wall(Move(bpos, rot + 1), rot),
            letter = _maze[pos.X][pos.Y].Label,
            letter_far = _maze[bpos.X][bpos.Y].Label != (char)0
        });
    }

    private bool Wall(Coordinate pos, int rot)
    {
        switch (rot % 4)
        {
            case 0:
                return _maze[pos.X][pos.Y].N;
            case 1:
                return _maze[(pos.X + 1) % 16][pos.Y].W;
            case 2:
                return _maze[pos.X][(pos.Y + 1) % 16].N;
            default:
                return _maze[pos.X][pos.Y].W;
        }
    }

    private Coordinate Move(Coordinate pos, int rot)
    {
        switch (rot % 4)
        {
            case 0:
                return new Coordinate(pos.X, (pos.Y + 15) % 16);
            case 1:
                return new Coordinate((pos.X + 1) % 16, pos.Y);
            case 2:
                return new Coordinate(pos.X, (pos.Y + 1) % 16);
            default:
                return new Coordinate((pos.X + 15) % 16, pos.Y);
        }
    }

    const float _holdDelay = 1f;

    private void HandlePress(int ix, bool pressIn)
    {
        if (pressIn)
        {
            KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[ix].transform);
            Buttons[ix].AddInteractionPunch(0.1f);
        }
        else if (ix == CodeStraight)
            KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, Buttons[ix].transform);

        if (!isActive || _isSolved)
            return;

        if (ix == CodeLeft && pressIn)
        {
            _rotA--;
            _rotB--;
            if (_rotA == -1)
                _rotA = 3;
            if (_rotB == -1)
                _rotB = 3;
            DisplayPlayer();
        }

        if (ix == CodeRight && pressIn)
        {
            _rotA++;
            _rotB++;
            _rotA %= 4;
            _rotB %= 4;
            DisplayPlayer();
        }

        if (ix == CodeStraight && pressIn)
        {
            _holdStart = Time.time;
            _holding = true;
            StartCoroutine(Hold());
        }

        if (ix == CodeStraight && !pressIn)
        {
            _holding = false;
            _greened = false;
            foreach (var mat in AllMaterials)
                mat.material.color = new Color32(255, 255, 255, 255);
            if (Time.time - _holdStart < _holdDelay)
            {
                if (!Wall(_playerA, _rotA))
                    _playerA = Move(_playerA, _rotA);
                if (!Wall(_playerB, _rotB))
                    _playerB = Move(_playerB, _rotB);
                DisplayPlayer();
            }
            else
            {
                if (_playerA == _playerB)
                {
                    Solve();
                }
                else
                {
                    var names = new string[] { "North", "East", "South", "West" };
                    Log("That was incorrect! One lover was at {0} (facing {2}) while the other was at {1} (facing {3}).", _playerA, _playerB, names[_rotA], names[_rotB]);
                    BombModule.HandleStrike();
                }
            }
        }
    }

    private void Solve(bool error = false, bool force = false)
    {
        if (error)
            Log("An error occurred, so the module will automatically solve.");
        else if (force)
        {
            KMAudio.PlaySoundAtTransform("solve", transform);
            Log("Module force solved.");
        }
        else
        {
            KMAudio.PlaySoundAtTransform("solve", transform);
            Log("Module solved.");
        }
        BombModule.HandlePass();
        UpdateDisplay(new MapView());
        _isSolved = true;
    }

    private IEnumerator Hold()
    {
        float t = _holdStart + _holdDelay;
        while (Time.time < t)
        {
            if (!_holding)
                yield break;
            yield return null;
        }

        if (!_holding)
            yield break;

        _greened = true;
        KMAudio.PlaySoundAtTransform("spark", transform);
        foreach (var mat in AllMaterials)
            mat.material.color = new Color32(200, 200, 200, 255);
    }

    private void UpdateDisplay(MapView view)
    {
        MR_ble.enabled = view.ble;
        MR_blw.enabled = view.blw;
        MR_bre.enabled = view.bre;
        MR_brw.enabled = view.brw;
        MR_bw.enabled = view.bw;

        MR_fle.enabled = view.fle;
        MR_flw.enabled = view.flw;
        MR_fre.enabled = view.fre;
        MR_frw.enabled = view.frw;
        MR_fw.enabled = view.fw;

        MR_letter_far.enabled = view.letter_far;
        MR_letter.enabled = true;

        switch (view.letter)
        {
            case 'A':
                MR_letter.material = MatA;
                break;
            case 'B':
                MR_letter.material = MatB;
                break;
            case 'C':
                MR_letter.material = MatC;
                break;
            case 'D':
                MR_letter.material = MatD;
                break;
            case 'H':
                MR_letter.material = MatH;
                break;
            case 'E':
                MR_letter.material = MatE;
                break;
            case 'N':
                MR_letter.material = MatN;
                break;
            case 'S':
                MR_letter.material = MatS;
                break;
            case 'W':
                MR_letter.material = MatW;
                break;
            default:
                MR_letter.enabled = false;
                break;
        }

        MR_letter.material.color = _greened ? new Color32(200, 200, 200, 255) : new Color32(255, 255, 255, 255);

        // Perform some wizardry because the game has very bizarre glitches
        MeshFilter mf = MR_letter.gameObject.GetComponent<MeshFilter>();

        Vector2[] uvs = mf.mesh.uv;
        uvs[0] = new Vector2(0.0f, 0.0f);
        uvs[1] = new Vector2(1.0f, 1.0f);
        uvs[2] = new Vector2(1.0f, 0.0f);
        uvs[3] = new Vector2(0.0f, 1.0f);
        mf.mesh.uv = uvs;
    }

    private KMSelectable[] ShortenDirection(string direction)
    {
        switch (direction)
        {
            case "l":
            case "left":
                return new[] { Buttons[0] };
            case "r":
            case "right":
                return new[] { Buttons[2] };
            case "f":
            case "forward":
                return new[] { Buttons[1] };
            case "u":
            case "u-turn":
            case "uturn":
            case "turnaround":
            case "turn-around":
                return new[] { Buttons[2], Buttons[2] };
            default:
                return new KMSelectable[] { null };
        }
    }

    private void TwitchHandleForcedSolve()
    {
        Solve(force: true);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "!{0} move L F R F U [move] [L = left, R = right, F = forward, U = u-turn] | !{0} submit";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string inputCommand)
    {
        List<string> commands = inputCommand.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        if (commands.Count == 0) yield break;

        if (ShortenDirection(commands[0])[0] == null)
        {
            switch (commands[0])
            {
                case "move":
                case "m":
                case "walk":
                case "w":
                    commands.RemoveAt(0);
                    break;
                case "submit":
                case "s":
                    if (commands.Count > 1)
                        goto default;
                    yield return null;
                    Buttons[1].OnInteract();
                    yield return new WaitForSeconds(_holdDelay + 1f);
                    Buttons[1].OnInteractEnded();
                    yield break;
                default:
                    yield return "sendtochaterror valid commands are m = \'Move\' and s = \'Submit\'. Valid movements are l = \'Left\', r = \'Right\', f = \'Forward\', or u = \'U-turn\'.";
                    yield break;
            }
        }

        List<KMSelectable> moves = commands.SelectMany(ShortenDirection).ToList();
        if (!moves.Any())
        {
            yield return "sendtochaterror Please tell me a set of moves.";
            yield break;
        }
        if (moves.Any(m => m == null))
        {
            string invalidMove = commands.FirstOrDefault(x => ShortenDirection(x)[0] == null);
            if (!string.IsNullOrEmpty(invalidMove))
                yield return string.Format("sendtochaterror I don't know how to move in the direction of {0}.", invalidMove);
            yield break;
        }
        yield return null;

        if (moves.Count > (64)) yield return "elevator music";

        const float moveDelay = 0.1f;
        foreach (KMSelectable move in moves)
        {
            move.OnInteract();
            move.OnInteractEnded();
            yield return "trycancel";
            yield return new WaitForSeconds(moveDelay);
        }
    }
}
