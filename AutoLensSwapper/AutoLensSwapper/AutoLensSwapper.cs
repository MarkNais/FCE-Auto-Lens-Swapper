using UnityEngine;

public class MyMod: FortressCraftMod
{
    //what is MachineType?
    public ushort alCubeType = ModManager.mModMappings.CubesByKey["MarkNstein.AutoLensSwapper"].CubeType;

    public override ModRegistrationData Register()
    {
        ModRegistrationData modRegData = new ModRegistrationData();
        modRegData.RegisterEntityHandler("MarkNstein.AutoLensSwapper");
        //modRegData.RegisterEntityUI("MarkNstein.AutoLensSwapper", new AutoLensMachineWindow());
        // ^^^ still need to make the window handler

        Debug.Log("MarkNstein.AutoLensSwapper registered. Version 1.");

        //UIManager.NetworkCommandFunctions.Add("MarkNstein.AutoLensSwapperInterface", new UIManager.HandleNetworkCommand(AutoLensMachineWindow.HandleNetworkCommand));
        // ^^^ Still need to make window handler, and network capabilities.

        return modRegData;
    }

    public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
    {
        ModCreateSegmentEntityResults result = new ModCreateSegmentEntityResults();

        if (parameters.Cube == alCubeType)
        {
            parameters.ObjectType = SpawnableObjectEnum.MatterMover;
            result.Entity = new ALS_MachineEntity(parameters);
        }
        return result;
    }
}
