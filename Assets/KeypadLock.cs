/*MESSAGE TO ANY FUTURE CODERS:
 PLEASE COMMENT YOUR WORK
 I can't stress how important this is especially with bomb types such as boss modules.
 If you don't it makes it realy hard for somone like me to find out how a module is working so I can learn how to make my own.
 Please comment your work.
 Short_c1rcuit*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;
using KModkit;

public class KeypadLock : MonoBehaviour {

	public KMBombInfo bomb;
	public KMAudio Audio;

	//character array to store the serial number
	char[] serial = new char[6];

	//Text on the display
	public TextMesh display;

	//The array of numbers that are faded
	int[] inputcode = new int[4];

	//The array that will store the final code
	int[] submitcode = new int[4];

	//random number used in creating the random number code.
	int currentrand;

	//List that helps generate the random number code
	List<int> numbers = new List<int>(10) { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

	//Array that holds the numbers on the keypad
	public TextMesh[] numbertext;

	//array for the buttons to be accessed
	public KMSelectable[] buttons;

	//1D array that stores the needed table
	int[] neededtable = new int[10];

	//2D array that stores all the tables
	int[,] tables = new int[10, 10]
	{{1,5,2,3,9,0,8,6,4,7}, {6,9,0,2,4,1,8,7,3,5}, {5,6,7,4,1,9,8,3,2,0}, {6,2,0,7,9,4,1,5,8,3}, {9,6,5,1,0,3,2,4,7,8}, {9,8,0,3,4,2,5,1,7,6}, {7,0,4,9,5,1,3,2,8,6}, {9,6,8,4,0,3,5,1,2,7}, {2,5,9,0,6,8,4,7,3,1}, {6,9,5,3,2,1,4,0,8,7}};

	//Number to store the table number in.
	int tablenum;

	//position in inputting the code in
	int inputpos;

	//logging
	static int moduleIdCounter = 1;
	int moduleId;
	private bool moduleSolved;

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Submit the answer with “!{0} (code)”. For example: “!{0} 2806” to input 2806.";
#pragma warning restore 414

	public KMSelectable[] ProcessTwitchCommand(string command)
	{
		command = command.ToLowerInvariant().Trim();

		if (Regex.IsMatch(command, @"^\d{4}"))
		{
			command = command.Substring(0).Trim();
			return new[] { buttons[int.Parse(command[0].ToString())], buttons[int.Parse(command[1].ToString())], buttons[int.Parse(command[2].ToString())], buttons[int.Parse(command[3].ToString())] };
		}
		return null;
	}

	IEnumerator TwitchHandleForcedSolve()
	{
		foreach (KMSelectable selectable in ProcessTwitchCommand(String.Concat(submitcode[(0 + inputpos) % 4],submitcode[(1 + inputpos) % 4],submitcode[(2 + inputpos) % 4],submitcode[(3 + inputpos) % 4])))
		{
			ButtonPress(selectable);
			yield return new WaitForSeconds(0.1f);
		}
	}

	void Awake()
	{
		moduleId = moduleIdCounter++;
		foreach (KMSelectable button in buttons)
		{
			KMSelectable pressedButton = button;
			button.OnInteract += delegate () { ButtonPress(pressedButton); return false; };
		}
	}

	// Use this for initialization
	void Start ()
	{
		//convers the serial number to a char array
		serial = bomb.GetSerialNumber().ToCharArray();

		//code generates the 4 random numbers to selected. When a number is selected it will remove it from the list so it can't be selected again.
		for (int i = 0; i < 4; i++)
		{
			currentrand = UnityEngine.Random.Range(0, numbers.Count);
			inputcode[i] = numbers[currentrand];
			numbertext[numbers[currentrand]].gameObject.SetActive(false);
			numbers.Remove(numbers[currentrand]);
		}

		//Logging
		Debug.LogFormat("[Keypad Lock #{0}] Your numbers are {1}, {2}, {3}, {4}.", moduleId, inputcode[0], inputcode[1], inputcode[2], inputcode[3]);

		//Gets the table number using the serial number and battery count
		tablenum = GetTableNum(bomb.GetBatteryCount());

		//Logging
		Debug.LogFormat("[Keypad Lock #{0}] Your table number is {1}", moduleId, tablenum);

		//Copies the correct table data to a 1D array (using 40 as Block copy uses bytes and each int is 4 bytes)
		System.Buffer.BlockCopy(tables, tablenum * 40, neededtable, 0, 40);

		//Gets the submit code
		submitcode = GetSubmitCode(inputcode, neededtable);

		//Logging
		Debug.LogFormat("[Keypad Lock #{0}] The needed code is {1}, {2}, {3}, {4}", moduleId, submitcode[0], submitcode[1], submitcode[2], submitcode[3]);
	}

	int GetTableNum(int seed)
	{
		int number;
		char character;
		for (int i = 0; i < 3; i++)
		{
			Debug.LogFormat("[Keypad Lock #{0}] Round {1}", moduleId, i + 1);
			//Step 1: Modulo the number by 6 (don't add 1 as arrays are 0 based)
			seed = seed % 6;
			Debug.LogFormat("[Keypad Lock #{1}] The serial # position is {0}", seed + 1, moduleId);
			//Step 2: Find the charcter in the serial number in the position you just worked out
			character = serial[seed];
			Debug.LogFormat("[Keypad Lock #{1}] Position {2} gets us {0}", character, moduleId, seed + 1);
			//Step 3: If the character is a letter then turn it into its alphanumeric position (A=1, B=2, etc)
			//Works out if the character is a letter
			bool success = Int32.TryParse(character.ToString(), out number);

			if (!success)
			{
				//If it is a letter then convert it to a number
				seed = (int)character - 64;
				Debug.LogFormat("[Keypad Lock #{2}] {0} as a number is {1}", character, seed, moduleId);
			}
			else
			{
				seed = number;
			}	
		}
		//Finaly, modulo the number by 10 to get the table needed
		seed = seed % 10;
		return seed;
	}

	int[] GetSubmitCode(int[] inputtable, int[] datatable)
	{
		//table to return at the end of the function
		int[] outputtable = new int[4];

		inputtable.CopyTo(outputtable, 0);

		for (int i = 0; i < 4; i++)
		{
			//sets all the values in the output table to the corresponing ones in the oreder table
			outputtable[i] = datatable[outputtable[i]];
		}

		//Sorts the items into correct order
		Array.Sort(outputtable);

		//Turns the numbers back to origanal
		for (int i = 0; i < 4; i++)
		{
			outputtable[i] = Array.IndexOf(datatable, outputtable[i]);
		}

		return outputtable;
	}

	void ButtonPress(KMSelectable button)
	{
		//Will only do somthing when the bomb is unsolved
		if (moduleSolved)
		{
			return;
		}

		//Makes the bomb move when you press it
		button.AddInteractionPunch();

		//Makes a sound when you press the button.
		GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

		//As the value of each button on the module is equivalent to their position in the array, I can get the button's position and use that to work out it's value.
		int number = Array.IndexOf(buttons, button);
	
		//if that is the correct button press
		if (submitcode[inputpos] == number)
		{
			//Removes the underscore at the current input position
			display.text = display.text.Remove(inputpos, 1);
			//Replaces the removed underscore with the inputted number
			display.text = display.text.Insert(inputpos, number.ToString());
				
			//Increments the input position by one
			inputpos += 1;

			//Logging
			Debug.LogFormat("[Keypad Lock #{0}] You submitted {1}. Correct.", moduleId, number);
		}
		else
		{
			//Resets the module
			inputpos = 0;
			display.text = "____";
			GetComponent<KMBombModule>().HandleStrike();
			Debug.LogFormat("[Keypad Lock #{0}] You submitted {1}. Incorrect. Resetting screen.", moduleId, number);
		}

		if (inputpos == 4)
		{
			//Solves the module
			moduleSolved = true;
			GetComponent<KMBombModule>().HandlePass();
			Debug.LogFormat("[Keypad Lock #{0}] Module solved", moduleId);
		}
		
	}
}
