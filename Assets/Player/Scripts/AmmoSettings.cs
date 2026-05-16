using UnityEngine;
using UnityEngine.InputSystem;

public class AmmoSettings : MonoBehaviour
{

    public int ammoCount = 100;
    public int maxAmmo = 100;
    public int magazineCount = 15;
    public int magazineMax = 15;

    public int getBulletsInMag()
    {
        if (magazineCount <= 0)
        {
            reload();
            return 0;
        }
        return magazineCount;  
    }
    public void shoot()
    {
        if (magazineCount > 0){
            magazineCount -=1;
        }
    }

    public void reload()
    {
        int delta = Mathf.Min(magazineMax - magazineCount, ammoCount);
        magazineCount += delta;
        ammoCount -= delta;
    }

    public void updateAmmoCount(int delta)
    {
        ammoCount += delta;
        if (ammoCount > maxAmmo) ammoCount=maxAmmo;
        if (ammoCount < 0) ammoCount = 0;
    }
}