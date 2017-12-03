/*
 * This is an example apparatus for DTW on compute shader. Inputs and templates could be loaded by putting them into folders (default or custom, just change the name). 
 */

using UnityEngine;
using System.Collections;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

public class ShaderManager : MonoBehaviour
{
    //Number of Templates. This and the number of threads needs to be the same to run.
    public Animator[] output = new Animator[5];
    int c = 0;

    static private int numOfTemplates = 27;

    //Length of template.
    static private int motionTemplateLength;

    //Length of Query/Input
    static private int motionQueryLength;

    //Number of bodyparts
    static private int numOfBodyParts = 6;

    static private int minindex = 0;

    static private string[] allGestures = new string[0];
    static private string[] input = new string[0];
    static private string[] gestures;

    static private string[] animFilesNames;
    public string[] animationNames;
    public int animIndex = 0;
    public bool sAnim = false;

    static GameObject[] tt;
    static GameObject[] t = new GameObject[0];

    //====================================================Compute shader stuff==================================================================
    static public int kiCalc;

    static public ComputeBuffer costMatrix;
    static public ComputeBuffer globalCostMatrix;
    static public ComputeBuffer motionTemplate;
    static public ComputeBuffer motionQuery;
    static public ComputeShader _shader;

    static float[] costMatrixArray = new float[numOfBodyParts * numOfTemplates * motionTemplateLength * motionQueryLength];
    static float[] globalCostMatrixArray = new float[numOfBodyParts * numOfTemplates * motionTemplateLength * motionQueryLength];

    static Quaternion[] motionTemplateArray = new Quaternion[numOfBodyParts * numOfTemplates * motionTemplateLength];

    static Quaternion[] motionQueryArray = new Quaternion[numOfBodyParts * motionQueryLength];

    static public ComputeBuffer shaderMin;
    static float[] shaderMinArray = new float[numOfTemplates * numOfBodyParts * 2];

    static public ComputeBuffer allMin;
    static int[] allMinArray = new int[numOfTemplates * numOfBodyParts];

    static ComputeBuffer dynamicTemplate;
    static int[] dynamicTemplateArray = new int[numOfBodyParts * numOfTemplates + 1];

    static ComputeBuffer sum;
    static int[] sumArray = new int[numOfBodyParts * numOfTemplates + 1];
    //===========================================================================================================================================

    static bool gestureGo = false;
    string[] tb;
    public List<Quaternion> inputGesture = new List<Quaternion>();
    static int currentInputIndex = motionQueryLength;
    bool start = false;
    bool done = true;

    //these arrays are for dynamic length input
    static int[] inputArrayCount = new int[numOfBodyParts * numOfTemplates + 1];
    static int[] inputArraySum = new int[numOfBodyParts * numOfTemplates + 1];


    static int[] bodypartsmin = new int[numOfBodyParts];
    static int[] mincounter = new int[numOfTemplates];

    public int[] bpmin = new int[numOfBodyParts];

    static int stopindex = 100000;
    static int templatecounter = 0;
    static int jumpin = 0;
    static double time;

    static bool rec = true;
    static bool go = false;

    public GameObject outavatar;
    AnimPlay anim;

    static int tc = 0;
    void Start()
    {
        anim = outavatar.GetComponent<AnimPlay>();
        tt = GameObject.FindGameObjectsWithTag("bodyparts");
        int k = 0;
        Array.Resize<GameObject>(ref t, tt.Length);
        while (k < 6)
        {
            foreach (GameObject g in tt)
            {
                if (g.name == "joint_Pelvis" && k == 0)
                {
                    t[k] = g;
                    k++;
                }
                if (g.name == "joint_Head" && k == 1)
                {
                    t[k] = g;
                    k++;
                }
                if (g.name == "joint_HandRT" && k == 2)
                {
                    t[k] = g;
                    k++;
                }
                if (g.name == "joint_HandLT" && k == 3)
                {
                    t[k] = g;
                    k++;
                }
                if (g.name == "joint_FootRT" && k == 4)
                {
                    t[k] = g;
                    k++;
                }
                if (g.name == "joint_FootLT" && k == 5)
                {
                    t[k] = g;
                    k++;
                }
                //Debug.Log(g + " " + g.transform.rotation);
            }
        }

        foreach(GameObject g in t)
        {
            Debug.Log("T " + g + " " + g.transform.rotation);
        }
        dynamicTemplateArray[0] = 0;
        sumArray[0] = 0;
        inputArrayCount[0] = 0;
        inputArraySum[0] = 0;
    }


    static void compute()
    {
        _shader = Resources.Load<ComputeShader>("csDTW");           // here we link computer shader code file to the shader class
        kiCalc = _shader.FindKernel("dtwCalc");                      // we retrieve kernel index by name from the code


        _shader.SetBuffer(kiCalc, "costMatrix", costMatrix);

        _shader.SetInt("numOfTemp", numOfTemplates);
        _shader.SetInt("tempLen", motionTemplateLength);
        _shader.SetInt("queryLength", motionQueryLength);
        _shader.SetInt("numOfBodyParts", numOfBodyParts);


        _shader.SetBuffer(kiCalc, "shaderMin", shaderMin);
        _shader.SetBuffer(kiCalc, "allMin", allMin);

        _shader.SetBuffer(kiCalc, "dynamicTemplate", dynamicTemplate);
        _shader.SetBuffer(kiCalc, "sum", sum);

        _shader.SetBuffer(kiCalc, "motionQuery", motionQuery);
        _shader.SetBuffer(kiCalc, "motionTemplate", motionTemplate);
        _shader.SetBuffer(kiCalc, "globalCostMatrix", globalCostMatrix);


        _shader.Dispatch(kiCalc, 27, 1, 1);


        globalCostMatrix.GetData(globalCostMatrixArray);
        costMatrix.GetData(costMatrixArray);


        shaderMin.GetData(shaderMinArray);
        allMin.GetData(allMinArray);
    }

    //function returns matched template, min and 'error'.
    void recognize()
    {

        float min = 100000000;
        for (int i = 0; i < numOfBodyParts * numOfTemplates * 2; i += 2)
        {

            if (min > shaderMinArray[1 + i] && shaderMinArray[1 + i] >= 0)
            {
                min = shaderMinArray[1 + i];
                minindex = (int)shaderMinArray[i];
            }
        }

        int counter = 1;

        int tindex = (minindex - motionQueryLength - 1) / motionQueryLength;


        if (tindex <= sumArray[counter])
        {
            //Debug.Log("Matched template is: " + (int)(Math.Ceiling((double)counter / 6)) + " " + shaderMinArray[2 * (counter - 1)] + " " + shaderMinArray[2 * (counter - 1) + 1] + " " + currentInputIndex);
            animIndex = (int)(Math.Ceiling((double)counter / t.Length)) - 1;

        }

        while (tindex > sumArray[counter])
        {

            if ((tindex) < sumArray[counter + 1])
            {
                //Debug.Log("Matched template is: " + (int)(Math.Ceiling((double)counter / 6)) + " " + " " + shaderMinArray[2 * (counter)] + " " + shaderMinArray[2 * (counter) + 1] + " " + currentInputIndex);
                animIndex = (int)(Math.Ceiling((double)counter / t.Length)) - 1;
                break;
            }
            counter++;
        }
    }

    void nRecognize()
    {
        Array.Clear(mincounter, 0, mincounter.Length);
        int bi = 0;
        for (int j = 0; j < numOfBodyParts; j++)
        {
            float min = 100000000;
            for (int i = 0; i < numOfTemplates; i++)
            {
                if (min > shaderMinArray[i * numOfBodyParts * 2 + 2 * j + 1] && shaderMinArray[i * numOfBodyParts * 2 + 2 * j + 1] >= 0)
                {
                    min = shaderMinArray[i * numOfBodyParts * 2 + 2 * j + 1];
                    minindex = (int)shaderMinArray[i * numOfBodyParts * 2 + 2 * j];
                }
            }

            int counter = 1;


            int tindex = (minindex - motionQueryLength - 1) / motionQueryLength;


            if (tindex <= sumArray[counter * numOfBodyParts + bi])
            {
                bodypartsmin[bi] = (counter % t.Length);
                bi++;
            }
            else if (tindex > sumArray[counter * numOfBodyParts + bi])
            {
                while (tindex > sumArray[counter * numOfBodyParts + bi])
                {
                    counter++;
                    if ((tindex < sumArray[counter * numOfBodyParts + bi + 1]))
                    {
                        bodypartsmin[bi] = (int)(Math.Ceiling((double)((counter * numOfBodyParts + bi) / t.Length)) + 1);
                        bi++;
                        break;
                    }

                }
            }
        }

        Array.Copy(bodypartsmin, bpmin, bodypartsmin.Length);
        string[] tb, tp;
        
        int[] oldbones = new int[numOfBodyParts];

            for(int i = 0; i < animationNames.Length; i++)
            {
                tp = animationNames[i].Split('.');
                output[0].SetFloat(tp[0], 0f);
                output[1].SetFloat(tp[0], 0f);
                output[2].SetFloat(tp[0], 0f);
                output[3].SetFloat(tp[0], 0f);
                output[4].SetFloat(tp[0], 0f);
            }
            for (int i = 1; i < bpmin.Length; i++)
            {
                tb = animationNames[bpmin[i]].Split('.');
                output[i-1].SetFloat(tb[0], 1f);
                //Debug.Log("CURRENT ANIMATION PLAYING: " + " " + tb[0]);
            }
        /*
        for (int i = 0; i < bodypartsmin.Length; i++)
        {
            if (bodypartsmin[i] == 1)
                mincounter[0]++;
            if (bodypartsmin[i] == 2)
                mincounter[1]++;
            if (bodypartsmin[i] == 3)
                mincounter[2]++;
            if (bodypartsmin[i] == 4)
                mincounter[3]++;
            if (bodypartsmin[i] == 5)
                mincounter[4]++;
            if (bodypartsmin[i] == 6)
                mincounter[5]++;
        }
        */
    }

    void getAnim()
    {
        animFilesNames = Directory.GetFiles(Application.dataPath + "/Models", "*.fbx");
        animationNames = Array.ConvertAll(animFilesNames, (s) => {
            var arr = s.Split('\\');
            return arr[arr.Length - 1];
        });
    }

    //reads all file in Template folder and save all lines into array
    void gesturefiles()
    {
        var FileNames = Directory.GetFiles(Application.dataPath + "/Template", "*.*");

        Array.Sort(FileNames);

        int newsize = allGestures.Length;
        string[] tb;

        if (FileNames == null)
        {
            Debug.Log("Didn't find any files");
            return;
        } // if

        int tcounter = 0;
        foreach (string str in FileNames)
        {
            
            if (str.Contains(".txt") && !str.Contains(".meta"))
            {
                //Debug.Log(str);
                gestures = File.ReadAllLines(str);

                //these two buffers are used for dynamic length templates
                dynamicTemplateArray[tcounter + 1] = gestures.Length;
                sumArray[tcounter + 1] = sumArray[tcounter] + dynamicTemplateArray[tcounter + 1];
                /*
                for (int i = 0; i < 1000; i++)
                {
                    //dynamicTemplateArray[tcounter + 1] = 120;//gestures.Length;
                    //sumArray[tcounter + 1] = sumArray[tcounter] + dynamicTemplateArray[tcounter + 1];
                    //dynamicTemplateArray[i + 1] = 240;//gestures.Length;
                    //sumArray[i + 1] = sumArray[i] + dynamicTemplateArray[i + 1];

                }
                */
                //get the size of the array and put it in as template length here
                //Array.Resize<string>(ref allGestures, newsize + 240000);//+gestures.length
                Array.Resize<string>(ref allGestures, newsize + gestures.Length);//+gestures.length

                for (int i = 0; i < gestures.Length; i++)
                {
                    allGestures[sumArray[tcounter] + i] = gestures[i];
                }
                tcounter++;
                newsize = allGestures.Length;
            }
            else if(str.Contains(".csv") && !str.Contains(".meta"))
            {
                Debug.Log(str);
                gestures = File.ReadAllLines(str);

                //these two buffers are used for dynamic length templates
                dynamicTemplateArray[tcounter + 1] = gestures.Length;
                sumArray[tcounter + 1] = sumArray[tcounter] + dynamicTemplateArray[tcounter + 1];

                Array.Resize<string>(ref allGestures, newsize + gestures.Length);//+gestures.length

                for (int i = 0; i < gestures.Length; i++)
                {
                    allGestures[sumArray[tcounter] + i] = "0,0,0,0";
                }
                tcounter++;
                newsize = allGestures.Length;
            }
        } // foreach

        motionTemplateArray = new Quaternion[allGestures.Length];

        //for (int i = 0; i < allGestures.Length; i++)
        for (int i = 0; i < allGestures.Length; i++)
        {
            tb = allGestures[i].Split(',');
            //Debug.Log(float.Parse(tb[0]) + " " + float.Parse(tb[1]) + " " + float.Parse(tb[2]) + " " + float.Parse(tb[3]));
            Quaternion q = new Quaternion(float.Parse(tb[0]), float.Parse(tb[1]), float.Parse(tb[2]), float.Parse(tb[3]));

            motionTemplateArray[i] = q;
        }


        motionTemplate = new ComputeBuffer(motionTemplateArray.Length, 4 * 4);
        motionTemplate.SetData(motionTemplateArray);


        shaderMin = new ComputeBuffer(shaderMinArray.Length, 4);
        allMin = new ComputeBuffer(allMinArray.Length, 4);

        dynamicTemplate = new ComputeBuffer(dynamicTemplateArray.Length, 4);
        sum = new ComputeBuffer(sumArray.Length, 4);
        dynamicTemplate.SetData(dynamicTemplateArray);
        sum.SetData(sumArray);

    }

    //reads all files in InputGestures folder and save all lines into array
    void getInput()
    {
        var FileNames = Directory.GetFiles(Application.dataPath + "/InputGestures", "*.txt");

        Array.Sort(FileNames);

        if (FileNames == null)
        {
            Debug.Log("Didn't find any files");
            return;
        } // if

        int newsize = input.Length;
        int tcounter = 0;

        /*
        gestures = File.ReadAllLines(FileNames[0]);
        Array.Resize<string>(ref input, newsize + gestures.Length);

        for (int i = 0; i < gestures.Length; i++)
        {
            input[i] = gestures[i];
        }*/
        
        foreach (string str in FileNames)
        {
            gestures = File.ReadAllLines(str);

            inputArrayCount[tcounter + 1] = gestures.Length;
            inputArraySum[tcounter + 1] = inputArraySum[tcounter] + gestures.Length;
            Array.Resize<string>(ref input, newsize + gestures.Length);

            for (int i = 0; i < gestures.Length; i++)
            {
                input[inputArraySum[tcounter] + i] = gestures[i];
            }

            tcounter++;
            newsize = input.Length;
        }
        

        motionQueryArray = new Quaternion[numOfBodyParts * motionQueryLength];
        costMatrixArray = new float[allGestures.Length * motionQueryLength];
        globalCostMatrixArray = new float[allGestures.Length * motionQueryLength];

        
        Array.Clear(motionQueryArray, 0, motionQueryArray.Length);
        for (int j = 0; j < numOfBodyParts; j++)
        {
            for (int i = 0; i < motionQueryLength; i++)
            {
                tb = input[inputArraySum[j] + i].Split(',');
                Quaternion q = new Quaternion(float.Parse(tb[0]), float.Parse(tb[1]), float.Parse(tb[2]), float.Parse(tb[3]));

                inputGesture.Add(q);
                motionQueryArray[j * motionQueryLength + i] = q;
            }
        }

        for (int i = 1; i < inputArrayCount.Length; i++)
        {
            if (stopindex > inputArrayCount[i] && inputArrayCount[i] > 50)
                stopindex = inputArrayCount[i];
        }

        motionQuery = new ComputeBuffer(motionQueryArray.Length, 4 * 4);
        motionQuery.SetData(motionQueryArray);


        globalCostMatrix = new ComputeBuffer(globalCostMatrixArray.Length, 4);
        globalCostMatrix.SetData(globalCostMatrixArray);

        costMatrix = new ComputeBuffer(costMatrixArray.Length, 4);
        costMatrix.SetData(costMatrixArray);

    }

    void initInput()
    {
        motionQueryArray = new Quaternion[t.Length * motionQueryLength];
        costMatrixArray = new float[allGestures.Length * motionQueryLength];
        globalCostMatrixArray = new float[allGestures.Length * motionQueryLength];

        motionQuery = new ComputeBuffer(motionQueryArray.Length, 4 * 4);


        globalCostMatrix = new ComputeBuffer(globalCostMatrixArray.Length, 4);
        globalCostMatrix.SetData(globalCostMatrixArray);

        costMatrix = new ComputeBuffer(costMatrixArray.Length, 4);
        costMatrix.SetData(costMatrixArray);
    }

    //copy input from list to query array 
    void inputingGesture()
    {
        for (int i = 0; i < motionQueryLength * numOfBodyParts; i++)
        {
            motionQueryArray[i] = inputGesture[i];
        }

        motionQuery.SetData(motionQueryArray);
    }

    void Update()
    {
        if (done)
        {
            time = (new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds;
            //this is queryLength. change it to however long a bone/bodypart is supposed to be
            motionQueryLength = 60;
            gesturefiles();
            initInput();
            getAnim();
            getInput();
            compute();
            recognize();
            //kinput();
            //currentInputIndex = motionQueryLength;
            StartCoroutine(wait());
            done = false;
            //start = true;
        }
        
        if (go)
        {
            for (int i = 0; i < t.Length; i++)
            {
                inputGesture.Add(t[i].transform.rotation);
                //File.AppendAllText("Walking.txt", t[i].transform.rotation.x.ToString() +","+ t[i].transform.rotation.y.ToString()+ "," + t[i].transform.rotation.z.ToString() + "," + t[i].transform.rotation.w.ToString() + "\n");
                //Debug.Log(t[i] + " " + t[i].transform.rotation);
            }
            if (inputGesture.Count > t.Length * motionQueryLength)
            {
                for (int i = 0; i < t.Length; i++)
                    inputGesture.RemoveAt(0);
            }

            if (inputGesture.Count == t.Length * motionQueryLength)
            {
                start = true;
            }

            if (start)
            {
                Array.Clear(motionQueryArray, 0, motionQueryArray.Length);
                for (int i = 0; i < motionQueryLength; i++)
                {
                    for (int j = 0; j < t.Length; j++)
                    {
                        motionQueryArray[j * motionQueryLength + i] = inputGesture[i];
                    }
                }
                motionQuery.SetData(motionQueryArray);
                //compute();
                //recognize();
                //nRecognize();
                StartCoroutine(cwait());
                //start = false;
                //Debug.Log((((new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds) - time).ToString());
                time = (new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds;
            }

        }
    }

    IEnumerator cwait()
    {
        yield return new WaitForSecondsRealtime(2f);
        compute();
        recognize();
        nRecognize();
        sAnim = true;
        start = false;
    }

    IEnumerator timer()
    {
        yield return new WaitForSecondsRealtime(10f);
        rec = false;
    }

    IEnumerator wait()
    {
        yield return new WaitForSecondsRealtime(3f);
        go = true;
    }
}
/*performance test
        for(int i = 0; i < numOfBodyParts; i++)
        {
            tb = input[currentInputIndex].Split(',');
            Quaternion q = new Quaternion(float.Parse(tb[0]), float.Parse(tb[1]), float.Parse(tb[2]), float.Parse(tb[3]));
            inputGesture.Add(t[i].transform.rotation);
            currentInputIndex++;
        }

        if (inputGesture.Count > t.Length * motionQueryLength)
        {
            for (int i = 0; i < t.Length; i++)
                inputGesture.RemoveAt(0);
        }

        if (inputGesture.Count == t.Length * motionQueryLength)
        {
            start = true;
        }

        if (start)
        {
            //time = (new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds;
            tc++;
            Array.Clear(motionQueryArray, 0, motionQueryArray.Length);
            for (int i = 0; i < motionQueryLength; i++)
            {
                for (int j = 0; j < t.Length; j++)
                {
                    motionQueryArray[j * motionQueryLength + i] = inputGesture[i];
                }
            }

            motionQuery.SetData(motionQueryArray);
            compute();
            //recognize();
            nRecognize();
            //if (sAnim == false)
                //StartCoroutine(cwait());
            start = false;
            //Debug.Log((((new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds) - time).ToString());
            File.AppendAllText("DTWTime.txt", (((new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds) - time).ToString() + " ");
            time = (new TimeSpan(DateTime.Now.Ticks)).TotalMilliseconds;
        }

        if(tc == 100)
        {
            Debug.LogError("DONE");
        }*/

/*foreach (GameObject g in t)
{
    Debug.Log(g + " " + g.transform.rotation);
}
//this if statement will fill the template buffer and query buffer and run dtw for however long motionQueryLength is.
if (done)
{
    //this is queryLength. change it to however long a bone/bodypart is supposed to be
    motionQueryLength = 20;
    gesturefiles();
    getInput();
    compute();
    recognize();
    currentInputIndex = motionQueryLength;

    done = false;
    start = true;
}

//this if statement will loop through the entire input frame by frame and perform dtw
if (start)
{
    if (inputGesture.Count == motionQueryArray.Length)
    {
        gestureGo = true;
    }

    //put the next frame into list for all bones
    for (int i = 0; i < numOfBodyParts; i++)
    {
        tb = input[inputArraySum[i + templatecounter*numOfBodyParts] + currentInputIndex].Split(',');
        Quaternion q = new Quaternion(float.Parse(tb[0]),float.Parse(tb[1]), float.Parse(tb[2]), float.Parse(tb[3]));
        inputGesture.Insert((i + 1) * (motionQueryLength)+i, q);
    }
    currentInputIndex++;

    //remove the first element from list
    if (inputGesture.Count > motionQueryLength)
    {
        for (int i = 0; i < numOfBodyParts; i++)
        {
            inputGesture.RemoveAt(i * (motionQueryLength));
        }
    }


    if (gestureGo)
    {
        //put items from list into query buffer
        inputingGesture();
        //pass it to shader for dtw
        compute();
        //find matching template
        recognize();

        gestureGo = false;
    }


    if(currentInputIndex == inputArrayCount[1])
    {
        Debug.LogError("Done");
        currentInputIndex = 0;
    }

}*/
