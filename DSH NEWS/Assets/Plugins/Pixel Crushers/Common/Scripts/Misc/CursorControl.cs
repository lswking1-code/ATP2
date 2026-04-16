// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;

namespace PixelCrushers
{

    /// <summary>
    /// Methods to hide and show the cursor.
    /// </summary>
    public static class CursorControl
    {

        public static CursorLockMode cursorLockMode { get; set; } = CursorLockMode.Locked;
        // 为 true 时，阻止 Pixel Crushers 插件在运行时自动改写光标状态。
        public static bool blockPluginCursorChanges { get; set; } = true;

        public static bool isCursorActive
        {
            get { return isCursorVisible && !isCursorLocked; }
        }

        public static void SetCursorActive(bool value)
        {
            ShowCursor(value);
            LockCursor(!value);
        }

		public static bool isCursorVisible
		{
			get { return Cursor.visible; }
		}
		
		public static bool isCursorLocked
		{
			get { return Cursor.lockState != CursorLockMode.None; }
		}
		
		public static void ShowCursor(bool value) 
		{
            if (blockPluginCursorChanges) return;
			Cursor.visible = value;
		}
		
		public static void LockCursor(bool value) 
		{
            if (blockPluginCursorChanges) return;
			if (value == false && isCursorLocked) 
			{
				cursorLockMode = Cursor.lockState;
			}
			Cursor.lockState = value ? cursorLockMode : CursorLockMode.None;
		}
		
    }

}
