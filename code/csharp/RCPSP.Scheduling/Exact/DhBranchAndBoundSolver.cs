// Thesis traceability: Modified DH Branch-and-Bound exact reference used for nominal-quality comparison.
using RCPSP.Scheduling.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace RCPSP.Scheduling.Exact
{
    public sealed class DhBranchAndBoundSolver
    {
        internal sealed class Activity
        {
            public int Id { get; }
            public double Duration { get; }
            public double[] Demand { get; }
            public List<int> Preds { get; }
            public List<int> Succs { get; }

            public Activity(int id, double duration, double[] demand, IEnumerable<int> preds)
            {
                Id = id;
                Duration = duration;
                Demand = (double[])demand.Clone();
                Preds = preds != null ? new List<int>(preds) : new List<int>();
                Succs = new List<int>();
            }
        }

        internal sealed class ScheduleState
        {
            public bool[] Started;
            public bool[] Completed;
            public bool[] Active;

            public double[] Start;
            public double[] Finish;

            public bool[][] ExtraPreds;

            public List<int> ActiveList;
            public double Time;
            public double LBp;

            public ScheduleState(int n)
            {
                Started = new bool[n];
                Completed = new bool[n];
                Active = new bool[n];

                Start = new double[n];
                Finish = new double[n];
                for (int i = 0; i < n; i++)
                {
                    Start[i] = double.PositiveInfinity;
                    Finish[i] = double.PositiveInfinity;
                }

                ExtraPreds = new bool[n][];
                for (int i = 0; i < n; i++)
                    ExtraPreds[i] = new bool[n];

                ActiveList = new List<int>(n);
                Time = 0.0;
                LBp = 0.0;
            }

            public ScheduleState DeepCopy()
            {
                int n = Started.Length;
                var c = new ScheduleState(n)
                {
                    Time = Time,
                    LBp = LBp,
                    ActiveList = new List<int>(ActiveList)
                };

                Array.Copy(Started, c.Started, n);
                Array.Copy(Completed, c.Completed, n);
                Array.Copy(Active, c.Active, n);
                Array.Copy(Start, c.Start, n);
                Array.Copy(Finish, c.Finish, n);

                for (int i = 0; i < n; i++)
                    Array.Copy(ExtraPreds[i], c.ExtraPreds[i], n);

                return c;
            }

            public int CompletedCount()
            {
                int count = 0;
                for (int i = 0; i < Completed.Length; i++)
                    if (Completed[i]) count++;
                return count;
            }
        }

        private readonly int _n;
        private readonly int _r;

        private readonly int[] _idsByIdx;
        private readonly Dictionary<int, int> _idxById;

        private readonly double[] _duration;
        private readonly double[][] _demand;
        private readonly int[][] _preds;
        private readonly int[][] _succs;

        private readonly double[] _capacity;
        private readonly int[] _topo;
        private readonly double[] _rcpl;

        private Dictionary<int, (double start, double end)> _bestSchedule;
        private double _bestMakespan;
        private double _bestSlackSum = double.NegativeInfinity;

        private readonly Dictionary<string, double> _visited = new Dictionary<string, double>();

        private Stopwatch _watch;
        private TimeSpan _timeLimit = TimeSpan.Zero;
        private bool _timeLimitReached;
        private long _nodesVisited;

        public bool WasTimeLimitReached => _timeLimitReached;
        public long NodesVisited => _nodesVisited;
        public double BestSlackSum => _bestSlackSum;

        private const double EPS = 1e-9;
        private const int TIME_QUANT = 1;


        private const bool USE_DOMINANCE = true;
        private const bool RESCON_COMPARE_BY_ACTIVITY_ID = true;
        private const bool RESCON_LEFTSHIFT_BY_ACTIVITY_ID = false;
        private const bool RESCON_OPTIONA_ORDER_BY_ACTIVITY_ID = false;
        private const bool RESCON_MDA_ORDER_BY_ACTIVITY_ID = false;

        private readonly SchedulingProjectData _data;
        private readonly BranchAndBoundMode _mode;


        private readonly bool _useDominance;

        private static double Q(double x)
        {
            return Math.Round(x / TIME_QUANT) * TIME_QUANT;
        }

        public DhBranchAndBoundSolver(
            SchedulingProjectData data,
            BranchAndBoundMode mode = BranchAndBoundMode.Classic)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _mode = mode;
            _useDominance = USE_DOMINANCE && mode != BranchAndBoundMode.ModifiedDh;
            _data = data;
            ValidateProjectData(_data);

            var acts = BuildActivities(_data);
            acts.Sort((a, b) => a.Id.CompareTo(b.Id));
            var capacity = BuildCapacity(_data);

            _n = acts.Count;
            _r = capacity.Length;

            _idsByIdx = new int[_n];
            _idxById = new Dictionary<int, int>(_n);

            for (int i = 0; i < _n; i++)
            {
                _idsByIdx[i] = acts[i].Id;
                _idxById[acts[i].Id] = i;
            }

            _duration = new double[_n];
            _demand = new double[_n][];
            _preds = new int[_n][];
            _succs = new int[_n][];
            _capacity = (double[])capacity.Clone();

            var succLists = new List<int>[_n];
            for (int i = 0; i < _n; i++)
                succLists[i] = new List<int>();

            for (int i = 0; i < _n; i++)
            {
                var a = acts[i];

                if (a.Demand.Length != _r)
                    throw new InvalidOperationException(
                        $"Activity {a.Id} requires {a.Demand.Length} resources, expected {_r}.");

                _duration[i] = a.Duration;
                _demand[i] = (double[])a.Demand.Clone();

                for (int k = 0; k < _r; k++)
                {
                    if (_demand[i][k] > _capacity[k] + EPS)
                        throw new InvalidOperationException(
                            $"Activity {a.Id} requires {_demand[i][k]} > capacidade[{k}]={_capacity[k]}.");
                }

                var predIdx = new List<int>(a.Preds.Count);
                for (int p = 0; p < a.Preds.Count; p++)
                {
                    int predId = a.Preds[p];
                    int predIndex;
                    if (!_idxById.TryGetValue(predId, out predIndex))
                        throw new InvalidOperationException($"Pred {predId} de {a.Id} inexistente.");

                    predIdx.Add(predIndex);
                    succLists[predIndex].Add(i);
                }

                _preds[i] = predIdx.ToArray();
            }

            for (int i = 0; i < _n; i++)
                _succs[i] = succLists[i].ToArray();

            _topo = ComputeTopoOrder();
            if (_topo.Length != _n)
                throw new InvalidOperationException("The graph contains a cycle.");

            _rcpl = new double[_n];
            ComputeRCPL();
        }

        public List<int> GetTopoOrderPublic()
        {
            var list = new List<int>(_n);
            for (int i = 0; i < _n; i++)
                list.Add(_idsByIdx[_topo[i]]);
            return list;
        }

        private static List<Activity> BuildActivities(SchedulingProjectData data)
        {
            var activities = data.GetNonSummaryActivities();
            var resources = data.Resources ?? new List<SchedulingResource>();
            var resourceIds = new List<int>(resources.Count);
            var seenResourceIds = new HashSet<int>();

            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource == null)
                    continue;

                if (seenResourceIds.Add(resource.Id))
                    resourceIds.Add(resource.Id);
            }

            resourceIds.Sort();

            var acts = new List<Activity>(activities.Count);

            for (int x = 0; x < activities.Count; x++)
            {
                var activity = activities[x];
                double[] demand = new double[resourceIds.Count];

                for (int y = 0; y < resourceIds.Count; y++)
                {
                    int resourceId = resourceIds[y];
                    int units;
                    demand[y] = activity.ResourceDemandByResourceId != null && activity.ResourceDemandByResourceId.TryGetValue(resourceId, out units)
                        ? units
                        : 0.0;
                }

                List<int> preds = activity.PredecessorIds != null
                    ? new List<int>(activity.PredecessorIds)
                    : new List<int>();

                acts.Add(new Activity(
                    id: activity.Id,
                    duration: activity.Duration,
                    demand: demand,
                    preds: preds));
            }

            return acts;
        }

        private static double[] BuildCapacity(SchedulingProjectData data)
        {
            var resources = data.Resources ?? new List<SchedulingResource>();
            var ordered = new List<SchedulingResource>(resources.Count);

            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource != null)
                    ordered.Add(resource);
            }

            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));

            double[] capacity = new double[ordered.Count];
            for (int x = 0; x < ordered.Count; x++)
                capacity[x] = ordered[x].Capacity;

            return capacity;
        }

        private static void ValidateProjectData(SchedulingProjectData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var activities = data.GetNonSummaryActivities();
            if (activities == null || activities.Count == 0)
                throw new InvalidOperationException("There are no loaded non-summary activities.");

            var activityIds = new HashSet<int>();
            for (int i = 0; i < activities.Count; i++)
                activityIds.Add(activities[i].Id);

            var resources = data.Resources ?? new List<SchedulingResource>();

            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                if (activity.Duration < 0)
                    throw new InvalidOperationException($"Invalid negative duration for activity {activity.Id}.");

                var predecessors = activity.PredecessorIds;
                if (predecessors == null)
                    continue;

                for (int p = 0; p < predecessors.Count; p++)
                {
                    int pred = predecessors[p];
                    if (!activityIds.Contains(pred))
                        throw new InvalidOperationException($"Activity {activity.Id} has a non-existent predecessor: {pred}.");
                }
            }

            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource != null && resource.Capacity < 0)
                    throw new InvalidOperationException($"Invalid negative capacity for resource {resource.Id}.");
            }
        }

        private int[] ComputeTopoOrder()
        {
            int[] indeg = new int[_n];
            for (int i = 0; i < _n; i++)
                indeg[i] = _preds[i].Length;

            Queue<int> q = new Queue<int>();
            for (int i = 0; i < _n; i++)
                if (indeg[i] == 0)
                    q.Enqueue(i);

            int[] order = new int[_n];
            int pos = 0;

            while (q.Count > 0)
            {
                int u = q.Dequeue();
                order[pos++] = u;

                var su = _succs[u];
                for (int s = 0; s < su.Length; s++)
                {
                    int v = su[s];
                    indeg[v]--;
                    if (indeg[v] == 0)
                        q.Enqueue(v);
                }
            }

            if (pos != _n)
                return new int[0];

            return order;
        }

        private void ComputeRCPL()
        {
            for (int t = _n - 1; t >= 0; t--)
            {
                int i = _topo[t];
                double bestSucc = 0.0;

                var si = _succs[i];
                for (int s = 0; s < si.Length; s++)
                {
                    int j = si[s];
                    if (_rcpl[j] > bestSucc)
                        bestSucc = _rcpl[j];
                }

                _rcpl[i] = _duration[i] + bestSucc;
            }
        }

        private bool IsEligible(ScheduleState s, int idx)
        {
            if (s.Started[idx] || s.Completed[idx])
                return false;

            var p = _preds[idx];
            for (int i = 0; i < p.Length; i++)
                if (!s.Completed[p[i]])
                    return false;

            var extra = s.ExtraPreds[idx];
            for (int i = 0; i < _n; i++)
                if (extra[i] && !s.Completed[i])
                    return false;

            return true;
        }

        private List<int> Eligible(ScheduleState s)
        {
            var result = new List<int>(_n);
            for (int i = 0; i < _n; i++)
                if (IsEligible(s, i))
                    result.Add(i);

            result.Sort((a, b) => _idsByIdx[a].CompareTo(_idsByIdx[b]));
            return result;
        }

        private double[] CurrentLoadVector(ScheduleState s)
        {
            var load = new double[_r];
            for (int i = 0; i < s.ActiveList.Count; i++)
            {
                int idx = s.ActiveList[i];
                var d = _demand[idx];
                for (int k = 0; k < _r; k++)
                    load[k] += d[k];
            }
            return load;
        }

        private double[] SumDemand(IList<int> set)
        {
            var sum = new double[_r];
            for (int i = 0; i < set.Count; i++)
            {
                int idx = set[i];
                var d = _demand[idx];
                for (int k = 0; k < _r; k++)
                    sum[k] += d[k];
            }
            return sum;
        }

        private double NextEventTime(ScheduleState s)
        {
            if (s.ActiveList.Count == 0)
                return s.Time;

            double best = double.PositiveInfinity;
            for (int i = 0; i < s.ActiveList.Count; i++)
            {
                int idx = s.ActiveList[i];
                if (s.Finish[idx] < best)
                    best = s.Finish[idx];
            }
            return best;
        }

        private void StartActivities(ScheduleState s, double time, IList<int> toStart)
        {
            if (toStart == null || toStart.Count == 0)
                return;

            time = Q(time);

            for (int i = 0; i < toStart.Count; i++)
            {
                int idx = toStart[i];
                if (s.Started[idx]) continue;

                s.Started[idx] = true;
                s.Active[idx] = true;
                s.Start[idx] = time;
                s.Finish[idx] = time + _duration[idx];
                s.ActiveList.Add(idx);
            }

            CompleteAt(s, time, true);
        }

        private void CompleteAt(ScheduleState s, double t, bool completeZeroOnly)
        {
            t = Q(t);

            for (int i = s.ActiveList.Count - 1; i >= 0; i--)
            {
                int idx = s.ActiveList[i];
                if (Math.Abs(Q(s.Finish[idx]) - t) < EPS)
                {
                    if (!completeZeroOnly || Math.Abs(_duration[idx]) < EPS)
                    {
                        s.Active[idx] = false;
                        s.Completed[idx] = true;
                        s.ActiveList.RemoveAt(i);
                    }
                }
            }
        }

        private void ApplyGstar(ScheduleState s, IList<int> dq, int jIdx)
        {
            for (int i = 0; i < dq.Count; i++)
                s.ExtraPreds[dq[i]][jIdx] = true;
        }

        private void DelaySet(ScheduleState s, IList<int> dq)
        {
            for (int i = 0; i < dq.Count; i++)
            {
                int idx = dq[i];
                if (!s.Started[idx]) continue;

                s.Started[idx] = false;
                if (s.Active[idx])
                {
                    s.Active[idx] = false;
                    s.ActiveList.Remove(idx);
                }

                s.Start[idx] = double.PositiveInfinity;
                s.Finish[idx] = double.PositiveInfinity;
            }
        }

        private double LowerBoundEnergy(ScheduleState s)
        {


            if (_mode == BranchAndBoundMode.ModifiedDh)
                return Q(s.Time);

            var energy = new double[_r];

            for (int i = 0; i < _n; i++)
            {
                if (s.Completed[i]) continue;

                double remDur;
                if (s.Active[i])
                    remDur = Math.Max(0.0, Q(s.Finish[i]) - Q(s.Time));
                else if (s.Started[i])
                    remDur = 0.0;
                else
                    remDur = _duration[i];

                var dem = _demand[i];
                for (int k = 0; k < _r; k++)
                    energy[k] += dem[k] * remDur;
            }

            double tail = 0.0;
            for (int k = 0; k < _r; k++)
            {
                if (_capacity[k] <= 0)
                    return double.PositiveInfinity;

                tail = Math.Max(tail, Math.Ceiling(energy[k] / _capacity[k]));
            }

            return Q(s.Time) + tail;
        }

        private string StateSignature(ScheduleState s)
        {
            var sb = new StringBuilder(_n * 8 + 64);

            sb.Append("C:");
            for (int i = 0; i < _n; i++)
                sb.Append(s.Completed[i] ? '1' : '0');

            sb.Append("|A:");
            for (int i = 0; i < _n; i++)
            {
                if (s.Active[i])
                {
                    sb.Append(i);
                    sb.Append(':');
                    sb.Append(Q(s.Finish[i] - s.Time));
                    sb.Append(';');
                }
            }

            sb.Append("|G:");
            for (int target = 0; target < _n; target++)
            {
                bool wroteTarget = false;
                for (int pred = 0; pred < _n; pred++)
                {
                    if (s.ExtraPreds[target][pred])
                    {
                        if (!wroteTarget)
                        {
                            sb.Append(target);
                            sb.Append('<');
                            wroteTarget = true;
                        }
                        sb.Append(pred);
                        sb.Append(',');
                    }
                }
                if (wroteTarget)
                    sb.Append(';');
            }

            return sb.ToString();
        }

        private bool Dominated(ScheduleState s)
        {
            string key = StateSignature(s);
            double bestTime;
            if (_visited.TryGetValue(key, out bestTime))
            {
                if (Q(s.Time) > Q(bestTime) + EPS)
                    return true;
            }

            if (!_visited.TryGetValue(key, out bestTime) || Q(s.Time) < Q(bestTime))
                _visited[key] = s.Time;

            return false;
        }


        private bool ShouldStop()
        {
            if (_timeLimit <= TimeSpan.Zero || _watch == null)
                return false;

            if (_watch.Elapsed >= _timeLimit)
            {
                _timeLimitReached = true;
                return true;
            }

            return false;
        }

        private int GetDummyStartIndex()
        {
            for (int i = 0; i < _n; i++)
            {
                if (_preds[i].Length == 0 && Math.Abs(_duration[i]) < EPS)
                    return i;
            }

            return _topo != null && _topo.Length > 0 ? _topo[0] : 0;
        }

        private void ComputeLqAndJ(
            IList<int> dq,
            bool[] inDq,
            IList<int> sunion,
            bool[] inE,
            double m,
            ScheduleState s,
            out double lq,
            out int jIdx)
        {
            double tj = double.PositiveInfinity;
            jIdx = -1;

            for (int x = 0; x < sunion.Count; x++)
            {
                int idx = sunion[x];
                if (inDq[idx]) continue;

                double fin = double.PositiveInfinity;
                if (s.Active[idx])
                    fin = Q(s.Finish[idx]);
                else if (inE[idx])
                    fin = Q(m) + _duration[idx];

                if (fin < tj)
                {
                    tj = fin;
                    jIdx = idx;
                }
            }

            if (jIdx < 0 || double.IsInfinity(tj))
            {


                jIdx = GetDummyStartIndex();
                tj = double.IsPositiveInfinity(s.Finish[jIdx]) ? 0.0 : Q(s.Finish[jIdx]);
            }

            double maxRcpl = 0.0;
            for (int i = 0; i < dq.Count; i++)
            {
                double v = _rcpl[dq[i]];
                if (v > maxRcpl) maxRcpl = v;
            }

            lq = Q(tj + maxRcpl);
        }

        private void LeftShiftNow(ScheduleState s)
        {
            var e = Eligible(s);
            if (e.Count == 0)
                return;

            var load = CurrentLoadVector(s);

            if (RESCON_LEFTSHIFT_BY_ACTIVITY_ID)
            {
                e.Sort((a, b) => _idsByIdx[a].CompareTo(_idsByIdx[b]));
            }
            else
            {
                e.Sort((a, b) =>
                {
                    int c = _rcpl[b].CompareTo(_rcpl[a]);
                    if (c != 0) return c;
                    return _idsByIdx[a].CompareTo(_idsByIdx[b]);
                });
            }

            var picked = new List<int>(e.Count);
            var sum = new double[_r];

            for (int p = 0; p < e.Count; p++)
            {
                int idx = e[p];
                bool fits = true;

                for (int k = 0; k < _r; k++)
                {
                    if (load[k] + sum[k] + _demand[idx][k] - _capacity[k] > EPS)
                    {
                        fits = false;
                        break;
                    }
                }

                if (!fits) continue;

                for (int k = 0; k < _r; k++)
                    sum[k] += _demand[idx][k];

                picked.Add(idx);
            }

            if (picked.Count > 0)
                StartActivities(s, s.Time, picked);
        }

        private List<List<int>> MDAsOnS_WithE(IList<int> sunion, bool[] inE, double[] overload, bool requireNewEligibleActivity)
        {
            var result = new List<List<int>>();
            var pick = new List<int>();
            var sum = new double[_r];

            void Add(int idx)
            {
                pick.Add(idx);
                for (int k = 0; k < _r; k++)
                    sum[k] += _demand[idx][k];
            }

            void RemoveLast()
            {
                int idx = pick[pick.Count - 1];
                for (int k = 0; k < _r; k++)
                    sum[k] -= _demand[idx][k];
                pick.RemoveAt(pick.Count - 1);
            }

            bool CoversAll()
            {
                for (int k = 0; k < _r; k++)
                    if (sum[k] < overload[k] - EPS)
                        return false;
                return true;
            }

            bool HasAnyE()
            {
                for (int i = 0; i < pick.Count; i++)
                    if (inE[pick[i]])
                        return true;
                return false;
            }

            bool IsMinimal()
            {
                for (int i = 0; i < pick.Count; i++)
                {
                    int idx = pick[i];
                    bool stillCover = true;
                    for (int k = 0; k < _r; k++)
                    {
                        if (sum[k] - _demand[idx][k] < overload[k] - EPS)
                        {
                            stillCover = false;
                            break;
                        }
                    }
                    if (stillCover)
                        return false;
                }
                return true;
            }

            void BT(int pos)
            {
                if (ShouldStop())
                    return;

                if (CoversAll())
                {
                    if ((!requireNewEligibleActivity || HasAnyE()) && IsMinimal())
                        result.Add(new List<int>(pick));
                    return;
                }

                if (pos >= sunion.Count)
                    return;

                Add(sunion[pos]);
                BT(pos + 1);
                RemoveLast();

                BT(pos + 1);
            }

            BT(0);
            return result;
        }

        private List<List<int>> FeasibleSubsetsOfE(IList<int> e, ScheduleState s)
        {
            var result = new List<List<int>>();
            var load = CurrentLoadVector(s);
            var current = new List<int>();
            var currentSum = new double[_r];

            void DFSBuild(int pos)
            {
                if (ShouldStop())
                    return;

                if (current.Count > 0)
                    result.Add(new List<int>(current));

                for (int i = pos; i < e.Count; i++)
                {
                    int idx = e[i];
                    bool fits = true;

                    for (int k = 0; k < _r; k++)
                    {
                        if (load[k] + currentSum[k] + _demand[idx][k] - _capacity[k] > EPS)
                        {
                            fits = false;
                            break;
                        }
                    }

                    if (!fits) continue;

                    current.Add(idx);
                    for (int k = 0; k < _r; k++)
                        currentSum[k] += _demand[idx][k];

                    DFSBuild(i + 1);

                    for (int k = 0; k < _r; k++)
                        currentSum[k] -= _demand[idx][k];
                    current.RemoveAt(current.Count - 1);
                }
            }

            DFSBuild(0);
            return result;
        }

        private double LowerBoundCutset(ScheduleState s, double lstarFromConflict = double.NegativeInfinity)
        {
            double lbEnergy = LowerBoundEnergy(s);
            double lbLstar = double.IsNegativeInfinity(lstarFromConflict) ? 0.0 : lstarFromConflict;
            return Q(Math.Max(lbEnergy, lbLstar));
        }

        private double ComputeScheduleSlackSum(Dictionary<int, (double start, double end)> schedule)
        {


            if (schedule == null || schedule.Count == 0)
                return 0.0;

            var startByIdx = new double[_n];
            var finishByIdx = new double[_n];

            for (int i = 0; i < _n; i++)
            {
                int actId = _idsByIdx[i];
                (double start, double end) se;
                if (!schedule.TryGetValue(actId, out se))
                    return 0.0;

                startByIdx[i] = Q(se.start);
                finishByIdx[i] = Q(se.end);
            }

            double makespan = 0.0;
            for (int i = 0; i < _n; i++)
                if (finishByIdx[i] > makespan)
                    makespan = finishByIdx[i];

            var ls = new double[_n];
            var lf = new double[_n];

            for (int t = _n - 1; t >= 0; t--)
            {
                int i = _topo[t];

                if (_succs[i].Length == 0)
                {
                    lf[i] = makespan;
                }
                else
                {
                    double minLS = double.PositiveInfinity;
                    var succ = _succs[i];
                    for (int s = 0; s < succ.Length; s++)
                    {
                        int j = succ[s];
                        if (ls[j] < minLS)
                            minLS = ls[j];
                    }

                    lf[i] = double.IsPositiveInfinity(minLS) ? makespan : minLS;
                }

                ls[i] = lf[i] - _duration[i];
            }

            double sumSlack = 0.0;
            for (int i = 0; i < _n; i++)
            {
                double slack = ls[i] - startByIdx[i];
                sumSlack += slack;
            }

            return Q(sumSlack);
        }


        private double ComputeResourceFeasibleSlackSum(Dictionary<int, (double start, double end)> schedule)
        {
            if (schedule == null || schedule.Count == 0)
                return 0.0;

            var start = new int[_n];
            var dur = new int[_n];

            for (int i = 0; i < _n; i++)
            {
                int actId = _idsByIdx[i];
                (double start, double end) se;
                if (!schedule.TryGetValue(actId, out se))
                    return 0.0;

                start[i] = (int)Q(se.start);
                dur[i] = (int)Q(_duration[i]);
            }

            int makespan = 0;
            for (int i = 0; i < _n; i++)
            {
                int fin = start[i] + dur[i];
                if (fin > makespan) makespan = fin;
            }

            long sumSlack = 0;
            for (int i = 0; i < _n; i++)
            {
                int finishI = start[i] + dur[i];


                int maxFinish = makespan;
                var succ = _succs[i];
                for (int sIdx = 0; sIdx < succ.Length; sIdx++)
                {
                    int st = start[succ[sIdx]];
                    if (st < maxFinish) maxFinish = st;
                }

                int slackI = 0;
                for (int t = finishI; t < maxFinish; t++)
                {
                    bool feasible = true;

                    for (int r = 0; r < _r; r++)
                    {
                        double res = _capacity[r];
                        for (int k = 0; k < _n; k++)
                        {
                            if (start[k] <= t && start[k] + dur[k] > t)
                                res -= _demand[k][r];
                        }
                        res -= _demand[i][r];
                        if (res < -EPS)
                        {
                            feasible = false;
                            break;
                        }
                    }

                    if (!feasible) break;
                    slackI++;
                }

                sumSlack += slackI;
            }

            return sumSlack;
        }

        private void TryUpdateBestSolution(
            Dictionary<int, (double start, double end)> candidateSchedule,
            double candidateMakespan)
        {
            if (candidateSchedule == null || candidateSchedule.Count == 0)
                return;

            if (_mode == BranchAndBoundMode.Classic)
            {
                if (candidateMakespan < _bestMakespan - EPS ||
                    (Math.Abs(candidateMakespan - _bestMakespan) <= EPS &&
                     _bestSchedule != null &&
                     CompareSchedulesLex(candidateSchedule, _bestSchedule) < 0))
                {
                    _bestMakespan = candidateMakespan;
                    _bestSchedule = new Dictionary<int, (double start, double end)>(candidateSchedule);
                    _bestSlackSum = ComputeScheduleSlackSum(candidateSchedule);
                }
                return;
            }


            double candidateSlackSum = ComputeResourceFeasibleSlackSum(candidateSchedule);

            if (candidateMakespan < _bestMakespan - EPS)
            {
                _bestMakespan = candidateMakespan;
                _bestSchedule = new Dictionary<int, (double start, double end)>(candidateSchedule);
                _bestSlackSum = candidateSlackSum;
                return;
            }

            if (Math.Abs(candidateMakespan - _bestMakespan) <= EPS)
            {
                if (candidateSlackSum > _bestSlackSum + EPS)
                {
                    _bestSchedule = new Dictionary<int, (double start, double end)>(candidateSchedule);
                    _bestSlackSum = candidateSlackSum;
                    return;
                }

                if (Math.Abs(candidateSlackSum - _bestSlackSum) <= EPS &&
                    _bestSchedule != null &&
                    CompareSchedulesLex(candidateSchedule, _bestSchedule) < 0)
                {
                    _bestSchedule = new Dictionary<int, (double start, double end)>(candidateSchedule);
                }
            }
        }

        private void DFS(ScheduleState s, int p, double lbPrev)
        {
            _nodesVisited++;
            if (ShouldStop())
                return;


            if (_mode == BranchAndBoundMode.Classic)
            {
                if (Q(s.Time) >= _bestMakespan) return;
                if (Q(lbPrev) >= _bestMakespan) return;
            }
            else
            {
                if (Q(s.Time) > _bestMakespan) return;
                if (Q(lbPrev) > _bestMakespan) return;
            }

            if (_useDominance && Dominated(s)) return;

            double lbEnergy0 = LowerBoundEnergy(s);

            if (_mode == BranchAndBoundMode.Classic)
            {
                if (Q(lbEnergy0) >= _bestMakespan) return;
            }
            else
            {
                if (Q(lbEnergy0) > _bestMakespan) return;
            }

            while (true)
            {
                if (ShouldStop())
                    return;

                if (_mode != BranchAndBoundMode.ModifiedDh)
                    LeftShiftNow(s);

                var e = Eligible(s);

                if (s.CompletedCount() == _n)
                {
                    var sched = new Dictionary<int, (double start, double end)>(_n);
                    double makespan = 0.0;

                    for (int i = 0; i < _n; i++)
                    {
                        double st = double.IsPositiveInfinity(s.Start[i]) ? 0.0 : Q(s.Start[i]);
                        double fn = double.IsPositiveInfinity(s.Finish[i]) ? 0.0 : Q(s.Finish[i]);
                        sched[_idsByIdx[i]] = (st, fn);
                        if (fn > makespan) makespan = fn;
                    }

                    TryUpdateBestSolution(sched, makespan);
                    return;
                }

                var loadNow = CurrentLoadVector(s);
                var demE = SumDemand(e);
                bool allECabem = true;

                for (int k = 0; k < _r; k++)
                {
                    if (loadNow[k] + demE[k] - _capacity[k] > EPS)
                    {
                        allECabem = false;
                        break;
                    }
                }

                if (e.Count > 0 && allECabem)
                {


                    if (_mode == BranchAndBoundMode.ModifiedDh)
                    {
                        StartActivities(s, s.Time, e);
                        continue;
                    }

                    var subsets = FeasibleSubsetsOfE(e, s);

                    if (RESCON_OPTIONA_ORDER_BY_ACTIVITY_ID)
                    {
                        subsets.Sort((sa, sb) =>
                        {
                            int na = sa.Count;
                            int nb = sb.Count;
                            int n = Math.Min(na, nb);

                            int c = CompareSubsetByActivityIds(sa, sb);
                            if (c != 0) return c;
                            return na.CompareTo(nb);
                        });
                    }
                    else
                    {
                        subsets.Sort((sa, sb) =>
                        {
                            double rcplA = 0.0, rcplB = 0.0;
                            int sumIdA = 0, sumIdB = 0;

                            for (int i = 0; i < sa.Count; i++)
                            {
                                rcplA += _rcpl[sa[i]];
                                sumIdA += _idsByIdx[sa[i]];
                            }
                            for (int i = 0; i < sb.Count; i++)
                            {
                                rcplB += _rcpl[sb[i]];
                                sumIdB += _idsByIdx[sb[i]];
                            }

                            int c = rcplB.CompareTo(rcplA);
                            if (c != 0) return c;

                            c = sb.Count.CompareTo(sa.Count);
                            if (c != 0) return c;

                            return sumIdA.CompareTo(sumIdB);
                        });
                    }

                    for (int i = 0; i < subsets.Count; i++)
                    {
                        if (ShouldStop())
                            return;
                        var child = s.DeepCopy();
                        StartActivities(child, child.Time, subsets[i]);

                        if (_mode != BranchAndBoundMode.ModifiedDh)
                            LeftShiftNow(child);

                        double next = NextEventTime(child);
                        child.Time = Q(next);
                        CompleteAt(child, child.Time, false);

                        double lbCut = LowerBoundCutset(child);

                        if (_mode == BranchAndBoundMode.Classic)
                        {
                            if (Q(child.Time) >= _bestMakespan) continue;
                            if (Q(lbCut) >= _bestMakespan) continue;
                        }
                        else
                        {
                            if (Q(child.Time) > _bestMakespan) continue;
                            if (Q(lbCut) > _bestMakespan) continue;
                        }

                        if (_useDominance && Dominated(child)) continue;

                        DFS(child, p + 1, Math.Max(lbPrev, lbCut));
                    }

                    return;
                }

                if (e.Count == 0 && s.ActiveList.Count > 0)
                {
                    double next = NextEventTime(s);
                    s.Time = Q(next);
                    CompleteAt(s, s.Time, false);

                    if (_mode != BranchAndBoundMode.ModifiedDh)
                        LeftShiftNow(s);

                    double lbCut = LowerBoundCutset(s);

                    if (_mode == BranchAndBoundMode.Classic)
                    {
                        if (Q(s.Time) >= _bestMakespan) return;
                        if (Q(Math.Max(lbPrev, lbCut)) >= _bestMakespan) return;
                    }
                    else
                    {
                        if (Q(s.Time) > _bestMakespan) return;
                        if (Q(Math.Max(lbPrev, lbCut)) > _bestMakespan) return;
                    }

                    if (_useDominance && Dominated(s)) return;

                    continue;
                }

                var sunion = new List<int>(s.ActiveList.Count + e.Count);
                bool[] inSunion = new bool[_n];
                bool[] inE = new bool[_n];

                for (int i = 0; i < s.ActiveList.Count; i++)
                {
                    int idx = s.ActiveList[i];
                    if (!inSunion[idx])
                    {
                        sunion.Add(idx);
                        inSunion[idx] = true;
                    }
                }

                for (int i = 0; i < e.Count; i++)
                {
                    int idx = e[i];
                    inE[idx] = true;
                    if (!inSunion[idx])
                    {
                        sunion.Add(idx);
                        inSunion[idx] = true;
                    }
                }

                var sumUnion = SumDemand(sunion);
                var overload = new double[_r];
                bool hasOver = false;

                for (int k = 0; k < _r; k++)
                {
                    overload[k] = Math.Max(0.0, sumUnion[k] - _capacity[k]);
                    if (overload[k] > EPS)
                        hasOver = true;
                }

                if (!hasOver)
                {
                    var child = s.DeepCopy();
                    StartActivities(child, child.Time, e);

                    if (_mode != BranchAndBoundMode.ModifiedDh)
                        LeftShiftNow(child);

                    double next = NextEventTime(child);
                    child.Time = Q(next);
                    CompleteAt(child, child.Time, false);

                    double lbCut2 = LowerBoundCutset(child);

                    bool passBound;
                    if (_mode == BranchAndBoundMode.Classic)
                        passBound = Q(child.Time) < _bestMakespan && Q(lbCut2) < _bestMakespan;
                    else
                        passBound = Q(child.Time) <= _bestMakespan && Q(lbCut2) <= _bestMakespan;

                    if (passBound && !(_useDominance && Dominated(child)))
                    {
                        DFS(child, p + 1, Math.Max(lbPrev, lbCut2));
                    }

                    return;
                }

                var mdas = MDAsOnS_WithE(sunion, inE, overload, _mode != BranchAndBoundMode.ModifiedDh);
                if (mdas.Count == 0)
                {
                    double next = NextEventTime(s);
                    s.Time = Q(Math.Max(s.Time, next));
                    CompleteAt(s, s.Time, false);
                    if (_mode != BranchAndBoundMode.ModifiedDh)
                        LeftShiftNow(s);

                    double lbCut3 = LowerBoundCutset(s);

                    if (_mode == BranchAndBoundMode.Classic)
                    {
                        if (Q(s.Time) >= _bestMakespan) return;
                        if (Q(Math.Max(lbPrev, lbCut3)) >= _bestMakespan) return;
                    }
                    else
                    {
                        if (Q(s.Time) > _bestMakespan) return;
                        if (Q(Math.Max(lbPrev, lbCut3)) > _bestMakespan) return;
                    }

                    if (_useDominance && Dominated(s)) return;

                    continue;
                }

                if (_mode == BranchAndBoundMode.ModifiedDh)
                {
                    mdas.Sort((a, b) => CompareSubsetByActivityIds(a, b));
                }

                var ranked = new List<(List<int> Dq, double Lq, int jIdx, int Order)>(mdas.Count);
                for (int i = 0; i < mdas.Count; i++)
                {
                    if (ShouldStop())
                        return;
                    var dq = mdas[i];
                    bool[] inDq = new bool[_n];
                    for (int d = 0; d < dq.Count; d++)
                        inDq[dq[d]] = true;

                    double lq;
                    int jIdx;
                    ComputeLqAndJ(dq, inDq, sunion, inE, s.Time, s, out lq, out jIdx);
                    ranked.Add((dq, lq, jIdx, mdas.Count - i));
                }

                if (_mode == BranchAndBoundMode.ModifiedDh)
                {


                    const int maxDA = 0x100;
                    ranked.Sort((a, b) =>
                    {
                        double ka = a.Lq * maxDA + a.Order;
                        double kb = b.Lq * maxDA + b.Order;
                        int c = ka.CompareTo(kb);
                        if (c != 0) return c;
                        return CompareSubsetByActivityIds(a.Dq, b.Dq);
                    });
                }
                else if (RESCON_MDA_ORDER_BY_ACTIVITY_ID)
                {
                    ranked.Sort((a, b) =>
                    {
                        int c = CompareSubsetByActivityIds(a.Dq, b.Dq);
                        if (c != 0) return c;
                        return a.Lq.CompareTo(b.Lq);
                    });
                }
                else
                {
                    ranked.Sort((a, b) =>
                    {
                        int c = a.Lq.CompareTo(b.Lq);
                        if (c != 0) return c;

                        c = a.Dq.Count.CompareTo(b.Dq.Count);
                        if (c != 0) return c;

                        double rcplA = 0.0, rcplB = 0.0;
                        int sumIdA = 0, sumIdB = 0;

                        for (int i = 0; i < a.Dq.Count; i++)
                        {
                            rcplA += _rcpl[a.Dq[i]];
                            sumIdA += _idsByIdx[a.Dq[i]];
                        }
                        for (int i = 0; i < b.Dq.Count; i++)
                        {
                            rcplB += _rcpl[b.Dq[i]];
                            sumIdB += _idsByIdx[b.Dq[i]];
                        }

                        c = rcplA.CompareTo(rcplB);
                        if (c != 0) return c;

                        return sumIdA.CompareTo(sumIdB);
                    });
                }

                double lstar = ranked[0].Lq;
                double lbHere = Math.Max(lbPrev, LowerBoundCutset(s, lstar));

                for (int i = 0; i < ranked.Count; i++)
                {
                    if (ShouldStop())
                        return;
                    var dq = ranked[i].Dq;
                    double lq = ranked[i].Lq;
                    int jIdx = ranked[i].jIdx;

                    if (jIdx < 0 || double.IsInfinity(lq)) continue;

                    if (_mode == BranchAndBoundMode.Classic)
                    {
                        if (Q(lq) >= _bestMakespan) continue;
                    }
                    else
                    {
                        if (Q(lq) > _bestMakespan) continue;
                    }

                    var child = s.DeepCopy();

                    ApplyGstar(child, dq, jIdx);
                    DelaySet(child, dq);

                    var toStart = new List<int>(e.Count);
                    bool[] inDq2 = new bool[_n];
                    for (int d = 0; d < dq.Count; d++)
                        inDq2[dq[d]] = true;

                    for (int ee = 0; ee < e.Count; ee++)
                    {
                        int idx = e[ee];
                        if (!inDq2[idx])
                            toStart.Add(idx);
                    }

                    StartActivities(child, child.Time, toStart);

                    if (_mode != BranchAndBoundMode.ModifiedDh)
                        LeftShiftNow(child);

                    double next = NextEventTime(child);
                    child.Time = Q(next);
                    CompleteAt(child, child.Time, false);

                    double childLB = Math.Max(lbHere, lq);
                    childLB = Math.Max(childLB, LowerBoundCutset(child));

                    if (_mode == BranchAndBoundMode.Classic)
                    {
                        if (Q(child.Time) >= _bestMakespan) continue;
                        if (Q(childLB) >= _bestMakespan) continue;
                    }
                    else
                    {
                        if (Q(child.Time) > _bestMakespan) continue;
                        if (Q(childLB) > _bestMakespan) continue;
                    }

                    if (_useDominance && Dominated(child)) continue;

                    DFS(child, p + 1, childLB);
                }

                return;
            }
        }

        private int CompareSubsetByActivityIds(IList<int> a, IList<int> b)
        {
            int countA = a == null ? 0 : a.Count;
            int countB = b == null ? 0 : b.Count;
            if (countA == 0 || countB == 0)
                return countA.CompareTo(countB);

            int[] idsA = new int[countA];
            int[] idsB = new int[countB];

            for (int i = 0; i < countA; i++)
                idsA[i] = _idsByIdx[a[i]];
            for (int i = 0; i < countB; i++)
                idsB[i] = _idsByIdx[b[i]];

            Array.Sort(idsA);
            Array.Sort(idsB);

            int n = countA < countB ? countA : countB;
            for (int i = 0; i < n; i++)
            {
                int c = idsA[i].CompareTo(idsB[i]);
                if (c != 0)
                    return c;
            }

            return countA.CompareTo(countB);
        }

        private struct ScheduleEntry
        {
            public int Id;
            public double Start;
            public double End;
        }

        private static ScheduleEntry[] BuildSortedScheduleEntries(Dictionary<int, (double start, double end)> schedule)
        {
            var entries = new ScheduleEntry[schedule.Count];
            int index = 0;
            foreach (var kv in schedule)
            {
                entries[index++] = new ScheduleEntry
                {
                    Id = kv.Key,
                    Start = Q(kv.Value.start),
                    End = Q(kv.Value.end)
                };
            }

            Array.Sort(entries, (a, b) =>
            {
                int c = a.Start.CompareTo(b.Start);
                if (c != 0) return c;
                c = a.End.CompareTo(b.End);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });

            return entries;
        }

        private static int CompareSchedulesLex(
            Dictionary<int, (double start, double end)> a1,
            Dictionary<int, (double start, double end)> a2)
        {
            if (RESCON_COMPARE_BY_ACTIVITY_ID)
            {
                int[] ids1 = new int[a1.Count];
                int[] ids2 = new int[a2.Count];
                int pos = 0;

                foreach (var id in a1.Keys)
                    ids1[pos++] = id;

                pos = 0;
                foreach (var id in a2.Keys)
                    ids2[pos++] = id;

                Array.Sort(ids1);
                Array.Sort(ids2);

                int n = ids1.Length < ids2.Length ? ids1.Length : ids2.Length;

                for (int i = 0; i < n; i++)
                {
                    int id1 = ids1[i];
                    int id2 = ids2[i];

                    int c = id1.CompareTo(id2);
                    if (c != 0) return c;

                    c = Q(a1[id1].start).CompareTo(Q(a2[id2].start));
                    if (c != 0) return c;

                    c = Q(a1[id1].end).CompareTo(Q(a2[id2].end));
                    if (c != 0) return c;
                }

                return ids1.Length.CompareTo(ids2.Length);
            }

            var o1 = BuildSortedScheduleEntries(a1);
            var o2 = BuildSortedScheduleEntries(a2);

            int m = o1.Length < o2.Length ? o1.Length : o2.Length;
            for (int i = 0; i < m; i++)
            {
                int c = o1[i].Start.CompareTo(o2[i].Start);
                if (c != 0) return c;

                c = o1[i].End.CompareTo(o2[i].End);
                if (c != 0) return c;

                c = o1[i].Id.CompareTo(o2[i].Id);
                if (c != 0) return c;
            }

            return o1.Length.CompareTo(o2.Length);
        }


        private void InitializeGreedyUpperBound()
        {


            var unscheduled = new HashSet<int>();
            for (int i = 0; i < _n; i++)
                unscheduled.Add(i);

            var scheduled = new HashSet<int>();
            var start = new double[_n];
            var finish = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                start[i] = double.PositiveInfinity;
                finish[i] = double.PositiveInfinity;
            }

            var usage = new Dictionary<int, double[]>();

            while (unscheduled.Count > 0)
            {
                var eligible = new List<int>();
                foreach (int idx in unscheduled)
                {
                    bool ok = true;
                    var preds = _preds[idx];
                    for (int p = 0; p < preds.Length; p++)
                    {
                        if (!scheduled.Contains(preds[p]))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok)
                        eligible.Add(idx);
                }

                if (eligible.Count == 0)
                    return;

                eligible.Sort((a, b) =>
                {
                    int cmp = _rcpl[b].CompareTo(_rcpl[a]);
                    if (cmp != 0) return cmp;
                    return _idsByIdx[a].CompareTo(_idsByIdx[b]);
                });

                int chosen = eligible[0];

                int earliest = 0;
                var cpreds = _preds[chosen];
                for (int p = 0; p < cpreds.Length; p++)
                {
                    int pred = cpreds[p];
                    int pf = (int)Q(finish[pred]);
                    if (pf > earliest) earliest = pf;
                }

                int dur = Math.Max(0, (int)Math.Ceiling(Q(_duration[chosen])));
                int st = earliest;
                while (!CanPlaceGreedy(usage, chosen, st, dur))
                    st++;

                start[chosen] = st;
                finish[chosen] = st + dur;

                for (int t = st; t < st + dur; t++)
                {
                    double[] load;
                    if (!usage.TryGetValue(t, out load))
                    {
                        load = new double[_r];
                        usage[t] = load;
                    }

                    var dem = _demand[chosen];
                    for (int k = 0; k < _r; k++)
                        load[k] += dem[k];
                }

                unscheduled.Remove(chosen);
                scheduled.Add(chosen);
            }

            var candidate = new Dictionary<int, (double start, double end)>(_n);
            double ms = 0.0;
            for (int i = 0; i < _n; i++)
            {
                candidate[_idsByIdx[i]] = (Q(start[i]), Q(finish[i]));
                if (finish[i] > ms) ms = finish[i];
            }

            TryUpdateBestSolution(candidate, Q(ms));
        }

        private bool CanPlaceGreedy(Dictionary<int, double[]> usage, int idx, int start, int duration)
        {
            if (duration <= 0)
                return true;

            var dem = _demand[idx];
            for (int t = start; t < start + duration; t++)
            {
                double[] load;
                usage.TryGetValue(t, out load);

                for (int k = 0; k < _r; k++)
                {
                    double current = load != null ? load[k] : 0.0;
                    if (current + dem[k] > _capacity[k] + EPS)
                        return false;
                }
            }

            return true;
        }

        public (Dictionary<int, (double start, double end)> schedule, double makespan, List<string> log) Run(int timeLimitSeconds = 10)
        {
            _bestSchedule = null;
            _bestMakespan = double.PositiveInfinity;
            _bestSlackSum = double.NegativeInfinity;
            _visited.Clear();
            _timeLimitReached = false;
            _nodesVisited = 0;
            _timeLimit = timeLimitSeconds > 0 ? TimeSpan.FromSeconds(timeLimitSeconds) : TimeSpan.Zero;
            _watch = Stopwatch.StartNew();


            if (_mode != BranchAndBoundMode.ModifiedDh)
                InitializeGreedyUpperBound();

            var s = new ScheduleState(_n) { Time = Q(0.0) };

            var sources = new List<int>();
            for (int i = 0; i < _n; i++)
                if (_preds[i].Length == 0)
                    sources.Add(i);

            sources.Sort((a, b) => _idsByIdx[a].CompareTo(_idsByIdx[b]));

            var zeroSources = new List<int>();
            for (int i = 0; i < sources.Count; i++)
            {
                int idx = sources[i];
                if (Math.Abs(_duration[idx]) < EPS)
                    zeroSources.Add(idx);
            }

            if (zeroSources.Count > 0)
            {
                StartActivities(s, 0.0, zeroSources);
                CompleteAt(s, 0.0, false);
            }

            double lb0 = 0.0;
            for (int i = 0; i < sources.Count; i++)
            {
                double v = _rcpl[sources[i]];
                if (v > lb0) lb0 = v;
            }
            s.LBp = lb0;

            DFS(s, 0, s.LBp);

            bool usedGreedyFallback = false;
            if (_bestSchedule == null)
            {


                InitializeGreedyUpperBound();
                usedGreedyFallback = _bestSchedule != null;
            }

            if (_bestSchedule == null)
            {
                var fallback = new Dictionary<int, (double start, double end)>(_n);
                double tfallback = 0.0;

                for (int i = 0; i < _n; i++)
                {
                    double st = double.IsPositiveInfinity(s.Start[i]) ? 0.0 : Q(s.Start[i]);
                    double fn = double.IsPositiveInfinity(s.Finish[i]) ? 0.0 : Q(s.Finish[i]);
                    fallback[_idsByIdx[i]] = (st, fn);
                    if (fn > tfallback) tfallback = fn;
                }

                var fallbackLog = new List<string>();
                if (_timeLimitReached) fallbackLog.Add("TimeLimit");
                fallbackLog.Add("NoCompleteBBSolution");
                fallbackLog.Add("Nodes=" + _nodesVisited);
                return (fallback, Q(tfallback), fallbackLog);
            }

            var quant = new Dictionary<int, (double start, double end)>(_bestSchedule.Count);
            foreach (var kv in _bestSchedule)
                quant[kv.Key] = (Q(kv.Value.start), Q(kv.Value.end));

            var log = new List<string>();
            if (_timeLimitReached)
                log.Add("TimeLimit");
            else
                log.Add("Optimal");
            if (usedGreedyFallback)
                log.Add("FallbackGreedy");
            log.Add("Nodes=" + _nodesVisited);
            log.Add("SlackSum=" + Q(_bestSlackSum));

            return (quant, Q(_bestMakespan), log);
        }

    }
}
