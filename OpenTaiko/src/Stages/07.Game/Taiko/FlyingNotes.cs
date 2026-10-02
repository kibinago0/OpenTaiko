using System.Runtime.InteropServices;
using FDK;

namespace OpenTaiko;

internal class FlyingNotes : CActivity {
	// Constructor

	public FlyingNotes() {
		base.IsDeActivated = true;
	}

	// 軌道座標データ (指定された全31点・30区間)
	private static readonly (double X, double Y)[] FlyingPath = new (double X, double Y)[]
	{
		(413, 257), (428, 228), (445, 200), (464, 174), (485, 148),
		(507, 124), (530, 102), (555, 81), (581, 62), (609, 45),
		(637, 29), (667, 16), (698, 5), (729, -4), (761, -11),
		(794, -15), (826, -18), (859, -18), (891, -16), (923, -13),
		(955, -7), (987, 1), (1018, 11), (1048, 24), (1077, 39),
		(1105, 56), (1132, 74), (1157, 95), (1181, 117), (1204, 140),
		(1225, 165)
	};


	// メソッド
	public virtual void Start(NotesManager.ENoteType nLane, EGameType gameType, int nPlayer, bool? forceFirework = null) {
		if (OpenTaiko.ConfigIni.nPlayerCount > 2 || OpenTaiko.ConfigIni.SimpleMode || nLane is NotesManager.ENoteType.Empty or NotesManager.ENoteType.Unknown)
			return;

		if (OpenTaiko.Tx.Notes[(int)gameType] != null) {
			for (int i = 0; i < 128; i++) {
				if (!Flying[i].IsUsing) {
					// 初期化
					Flying[i].IsUsing = true;
					Flying[i].Lane = nLane;
					Flying[i].GameType = gameType;
					Flying[i].Player = nPlayer;
					Flying[i].X = -100; //StartPointX[nPlayer];
					Flying[i].Y = -100; //TJAPlayer3.Skin.Game_Effect_FlyingNotes_StartPoint_Y[nPlayer];
					Flying[i].StartPointX = StartPointX[nPlayer];
					Flying[i].StartPointY = OpenTaiko.Skin.Game_Effect_FlyingNotes_StartPoint_Y[nPlayer];
					Flying[i].OldValue = 0;
					Flying[i].ForceFirework = forceFirework; // for balloons; no firework for big roll in some style (not followed)
					// 角度の決定
					Flying[i].Height = Math.Abs(OpenTaiko.Skin.Game_Effect_FlyingNotes_EndPoint_Y[nPlayer] - OpenTaiko.Skin.Game_Effect_FlyingNotes_StartPoint_Y[nPlayer]);
					Flying[i].Width = (Math.Abs((OpenTaiko.Skin.Game_Effect_FlyingNotes_EndPoint_X[nPlayer] - StartPointX[nPlayer])) / 2);
					//Console.WriteLine("{0}, {1}", width2P, height2P);
					Flying[i].Theta = ((Math.Atan2(Flying[i].Height, Flying[i].Width) * 180.0) / Math.PI);

					// タイマーの初期化: 全30区間 x 16.67ms = 約500.1ms (0〜500の範囲で1ms刻み)
					Flying[i].Counter = new CCounter(0, 500, 1, OpenTaiko.Timer);

					Flying[i].IncreaseX = (1.00 * Math.Abs((OpenTaiko.Skin.Game_Effect_FlyingNotes_EndPoint_X[nPlayer] - StartPointX[nPlayer]))) / (180);
					Flying[i].IncreaseY = (1.00 * Math.Abs((OpenTaiko.Skin.Game_Effect_FlyingNotes_EndPoint_Y[nPlayer] - OpenTaiko.Skin.Game_Effect_FlyingNotes_StartPoint_Y[nPlayer]))) / (180);
					break;
				}
			}
		}
	}

	// CActivity 実装

	public override void Activate() {
		for (int i = 0; i < 128; i++) {
			Flying[i] = new Status();
			Flying[i].IsUsing = false;
			Flying[i].Counter = new CCounter();
		}
		for (int i = 0; i < 2; i++) {
			StartPointX[i] = OpenTaiko.Skin.Game_Effect_FlyingNotes_StartPoint_X[i];
		}
		base.Activate();
	}
	public override void DeActivate() {
		for (int i = 0; i < 128; i++) {
			Flying[i].Counter = null;
		}
		base.DeActivate();
	}
	public override void CreateManagedResource() {
		base.CreateManagedResource();
	}
	public override void ReleaseManagedResource() {
		base.ReleaseManagedResource();
	}
	public override int Draw() {
		if (!base.IsDeActivated && !OpenTaiko.ConfigIni.SimpleMode) {
			for (int i = 0; i < 128; i++) {
				if (Flying[i].IsUsing) {
					Flying[i].OldValue = Flying[i].Counter.CurrentValue;
					Flying[i].Counter.Tick();
					if (Flying[i].Counter.IsEnded) {
						Flying[i].Counter.Stop();
						Flying[i].IsUsing = false;
						OpenTaiko.stageGameScreen.actGauge.Start(Flying[i].Lane, Flying[i].GameType, ENoteJudge.Perfect, Flying[i].Player);
						OpenTaiko.stageGameScreen.actChipEffects.Start(Flying[i].Player, Flying[i].Lane, Flying[i].GameType);
					}

					// 16.67msごとに指定座標間を直線移動（線形補間）
					double totalTimeMs = Flying[i].Counter.CurrentValue; // 0 ～ 500 ms
					double segmentTimeMs = 16.67; // 1区間あたりの時間

					int segmentIndex = (int)(totalTimeMs / segmentTimeMs);
					if (segmentIndex >= FlyingPath.Length - 1) {
						segmentIndex = FlyingPath.Length - 2;
					}

					double t = (totalTimeMs - (segmentIndex * segmentTimeMs)) / segmentTimeMs;
					if (t < 0) t = 0;
					if (t > 1) t = 1;

					var pStart = FlyingPath[segmentIndex];
					var pEnd = FlyingPath[segmentIndex + 1];

					double currentX = pStart.X + (pEnd.X - pStart.X) * t;
					double currentY = pStart.Y + (pEnd.Y - pStart.Y) * t;

					Flying[i].X = currentX + OpenTaiko.stageGameScreen.GetJPOSCROLLX(Flying[i].Player);
					Flying[i].Y = currentY + OpenTaiko.stageGameScreen.GetJPOSCROLLY(Flying[i].Player);

					// 花火・パーティクル発生判定
					for (int n = Flying[i].OldValue; n < Flying[i].Counter.CurrentValue; n += 16) {
						if (n % OpenTaiko.Skin.Game_Effect_FireWorks_Timing == 0 && Flying[i].Counter.CurrentValue > 18) {
							if (Flying[i].ForceFirework ?? NotesManager.IsBigNoteTaiko(Flying[i].Lane, Flying[i].GameType)) {
								OpenTaiko.stageGameScreen.FireWorks.Start(Flying[i].Lane, Flying[i].GameType, Flying[i].Player, Flying[i].X, Flying[i].Y);
							}
						}
					}

					NotesManager.DisplayNote(Flying[i].Player, (int)Flying[i].X, (int)Flying[i].Y, Flying[i].Lane, Flying[i].GameType);
				}
			}
		}
		return base.Draw();
	}


	#region [ private ]
	//-----------------

	[StructLayout(LayoutKind.Sequential)]
	private struct Status {
		public NotesManager.ENoteType Lane;
		public EGameType GameType;
		public int Player;
		public bool IsUsing;
		public CCounter Counter;
		public int OldValue;
		public double X;
		public double Y;
		public int Height;
		public int Width;
		public double IncreaseX;
		public double IncreaseY;
		public bool? ForceFirework;
		public int StartPointX;
		public int StartPointY;
		public double Theta;
	}

	private Status[] Flying = new Status[128];

	public readonly int[] StartPointX = new int[2];

	//-----------------
	#endregion
}
