using System;
using System.Collections.Generic;

[Serializable]
public class TspNodeData
{
    public float x;
    public float y;
}

[Serializable]
public class TspPuzzleData
{
    public int id;
    public List<TspNodeData> nodes;
    public List<int> optimalPath;
}

[Serializable]
public class TspPuzzleDatabase
{
    public List<TspPuzzleData> puzzles;
}