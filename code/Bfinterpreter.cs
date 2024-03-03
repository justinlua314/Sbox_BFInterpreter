using Sandbox;
using Sandbox.UI;
using System.Diagnostics;
using System;
using System.Threading.Tasks;

public sealed class Bfinterpreter : Component {
	public string Code { get; set; }

	public string StdOutString = "";

	public char InputChar = new();
	public bool AwaitingInput = false;

	public List<byte> Memory { get; set; } = new();

	public List<int> LoopPoints { get; set; } = new();

	public int LoopPointer = 0;
	public int LastLoop = 0;
	public bool SkippingLoop = false;

	public int MemPointer = 0;
	public int InsPointer = 0;

	/*
	* 0 = Ascii Chart
	* 1 = Documentation
	*/
	public int InfoState = 1;

	public bool Debug = false;
	public bool DebugStep = false;
	public bool DebugSkipping = false;
	int DebugSkipLoop = 0;

	/// <summary>
	/// If this many tokens are interpreted inside of a loop, close the script to prevent infinite loops crashing S&Box entirely
	/// </summary>
	[Property]
	public int LoopTokenLimit = 100000;
	int IterationsInLoop = 0;

	/// <summary>
	/// If this many tokens are interpreted at all, close the script to prevent infinite loops (Hack)
	/// </summary>
	[Property]
	public int CycleLimit = 1000000;
	int Cycles = 0;

	public void MoveRight() {
		MemPointer++;

		if (MemPointer == Memory.Count) {
			Memory.Add(0);
		}
	}

	public void MoveLeft() {
		MemPointer--;

		if (MemPointer == -1) {
			MemPointer = Memory.Count - 1;
		}
	}

	public void Add() {
		Memory[MemPointer]++;
	}

	public void Sub() {
		Memory[MemPointer]--;
	}

	public void ResetInterpreter() {
		MemPointer = 0;
		InsPointer = 0;
		Memory = [0];
		LoopPoints = [0];
		LoopPointer = 0;
		LastLoop = 0;
		SkippingLoop = false;
		StdOutString = "";
		DebugStep = false;
		DebugSkipping = false;
		DebugSkipLoop = 0;
	}

	public void CompileCode() {
		string compiled = "";
		string valid_tokens = "+-<>[].,";

		for (int index = 0; index < Code.Length; index++) {
			if ( valid_tokens.Contains(Code[index]) ) {
				compiled += Code[index];
			}
		}

		Clipboard.SetText(compiled);
	}

	public void SkipLoopInterpret( char command ) {
		switch ( command ) {
			case '[': LoopPointer++; break;
			case ']':
				LoopPointer--;

				if ( LoopPointer < LastLoop ) {
					SkippingLoop = false;
				}

				if (LoopPointer == 0 ) {
					IterationsInLoop = 0;
				}

				break;
		}
	}

	async public void StandardInterpret( char command ) {
		if (Debug && !DebugSkipping && !SkippingLoop) {
			while (Debug && !DebugStep) {
				await Task.Delay( 25 );
			}

			DebugStep = false;
		}

		switch ( command ) {
			case '+': Add(); break;
			case '-': Sub(); break;
			case '>': MoveRight(); break;
			case '<': MoveLeft(); break;
			case '.': StdOutString += (char)Memory[MemPointer]; break;
			case ',': AwaitingInput = true; break;

			case '[':
				LoopPointer++;

				if (LoopPointer >= LoopPoints.Count) { LoopPoints.Add(0); }

				if ( Memory[MemPointer] == 0 ) {
					LastLoop = LoopPointer;
					SkippingLoop = true;
				} else {
					LoopPoints[LoopPointer] = InsPointer;
				}
				break;

			case ']':
				if ( Memory[MemPointer] == 0 ) {
					LoopPointer--;
				} else {
					InsPointer = LoopPoints[LoopPointer];
				}
				break;
		}
	}

	async public void Run() {
		ResetInterpreter();
		char command;

		while ( InsPointer < Code.Length ) {
			command = Code[InsPointer];

			if ( DebugSkipping ) {
				if ( command == '[' ) {
					DebugSkipLoop++;
				} else {
					DebugSkipLoop = Math.Min(DebugSkipLoop - 1, 0);

					if ( DebugSkipLoop == 0 ) {
						if ( Memory[MemPointer] == 0 ) {
							DebugSkipping = false;
						} else {
							DebugSkipLoop++;
						}
					}
				}
			} else {
				while (Debug && !DebugStep) {
					await Task.Delay( 25 );
				}
			}

			if (command == '#') {
				while ( Code[InsPointer] != '\n' && InsPointer < Code.Length ) {
					InsPointer++;
				}

				continue;
			}

			if (SkippingLoop) {
				SkipLoopInterpret( command );
			} else {
				StandardInterpret( command );
			}

			if (LoopPointer != 0 ) {
				IterationsInLoop++;

				if (IterationsInLoop == LoopTokenLimit ) {
					Log.Warning( "BF Interpreter bailed on script to avoid infinite loop crash" );
					break;
				}
			}

			Cycles++;

			if (Cycles == CycleLimit) {
				Log.Warning( "BF Interpreter bailed on script to avoid infinite loop crash" );
				Log.Warning( "If you do not have infinite loops in your code, cosider writing a smaller script" );
				break;
			}

			while (AwaitingInput) {
				await Task.Delay( 25 );
			}

			InsPointer++;
			if (InsPointer == Code.Length) { break; }
		}

		DebugSkipLoop = 0;
		DebugSkipping = false;
	}

	public string CellRepr(int index) {
		if (index > Memory.Count) { return ""; }

		byte value = Memory[index];
		string render = value.ToString();

		if (index == MemPointer) {
			render = "[" + render + "]";

			if ( value < 100 ) {
				render = " " + render;

				if ( value < 10 ) {
					render += " ";
				}
			}
		} else {
			render = " " + render + " ";

			if ( value < 100 ) {
				render = " " + render;

				if ( value < 10 ) {
					render += " ";
				}
			}
		}

		return render;
	}

	protected override Task OnLoad() {
		Memory.Add( 0 );
		Components.Get<Documentation>( true ).Enabled = false;
		Components.Get<AsciiChart>( true ).Enabled = false;
		return base.OnLoad();
	}

	/*
	protected override void OnUpdate() {
		if ( Input.Pressed( "Left" ) ) {
			MoveLeft();
		}

		if (Input.Pressed("Right")) {
			MoveRight();
		}

		if ( Input.Pressed( "Forward" ) ) {
			Add();
		}

		if ( Input.Pressed( "Backward" ) ) {
			Sub();
		}
	}
	*/
}
