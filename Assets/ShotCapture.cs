using UnityEngine;

public class ShotCapture : MonoBehaviour
{
	void Update()
	{
		if (Input.GetKeyDown(KeyCode.F12))
			ScreenCapture.CaptureScreenshot("noir_VS_shot.png", 3);
	}
}