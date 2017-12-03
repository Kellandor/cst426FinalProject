using UnityEngine;
using System.Collections;

public class ForegroundToImage : MonoBehaviour 
{
	private GUITexture guiTex;

	void Start()
	{
		guiTex = GetComponent<GUITexture>();
	}

	void Update () 
	{
		if (guiTex && guiTex.texture == null) 
		{
			BackgroundRemovalManager backManager = BackgroundRemovalManager.Instance;
			KinectManager kinectManager = KinectManager.Instance;

			if (backManager && backManager.IsBackgroundRemovalInitialized()) 
			{
				guiTex.texture = backManager.GetForegroundTex();  // user's foreground texture
			} 
			else if(kinectManager && kinectManager.IsInitialized())
			{
				guiTex.texture = kinectManager.GetUsersClrTex();  // color camera texture
			}
		}
	}

}
