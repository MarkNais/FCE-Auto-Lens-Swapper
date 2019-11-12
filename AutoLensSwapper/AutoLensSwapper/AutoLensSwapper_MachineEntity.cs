using System.Collections.Generic;
using System.Linq;
using System.Text;
using FortressCraft.Community.Utilities;
using System.IO;
using UnityEngine;

/*To-do
 * Test ItemStack.amount : 0
 * Remove TargetLens and simplify PopUp
 * Debug Shift+E triggering !Shift-E
 * Add window
 */


public class ALS_MachineEntity : MachineEntity, PowerConsumerInterface, ItemConsumerInterface
{
    public int data;
    public GameObject HoloCubePreview;
    public bool mbLinkedToGo;
    private int mUpdates;
    private const bool mDebug = false;
    
    public eStatuses mStatus;
    public eIssues mIssue;
    public bool mbHaltedEarly;
    public bool mbOnlySwap;
    public bool mbTrashOld;

    private int mnSegmentID;
    private int mnSegmentEIndex1, mnSegmentEIndex2;
    private LaserPowerTransmitter mLastLPT;
    
    private int mnTrackLPTs;
    private int mnTrackSwaps;

    //private int mnValidHoppers;
    //private StorageMachineInterface[] maAttachedHoppers;
    //private List<StorageMachineInterface> maAttachedHoppers;
    public ItemBase mTargetLens;
    public ItemBase mStoredLenses;
    public const int minLensID = 3004;
    public const int maxLensID = 3014;

    public const int mnStorageMax = 1000;
    public const float mrMaxPower = 1500f;
    public const float mrMaxTransferRate = float.MaxValue;
    public const float mrPowerPerSwap = 64f;
    public float mrCurrentPower;

    public enum eStatuses
    {
        Stopped,
        Running,
        Done
    }

    public enum eIssues
    {
        Ready,
        Power,
        Input,
        Output,
        SetLens
    }


    public ALS_MachineEntity(ModCreateSegmentEntityParameters parameters)
    : base(parameters)
    {
        mbNeedsLowFrequencyUpdate = true;
        mbNeedsUnityUpdate = true;
        mUpdates = 0;

        mStatus = eStatuses.Stopped;
        mIssue = eIssues.Power;
        mbHaltedEarly = false;
        mbOnlySwap = false;
        mbTrashOld = false;

        mnSegmentID = 0;
        mnSegmentEIndex1 = 0;
        mnSegmentEIndex2 = 0;

        mnTrackLPTs = 0;
        mnTrackSwaps = 0;
        
        mrCurrentPower = 0;

        //maAttachedHoppers = new List<StorageMachineInterface>();

    }

    public override void DropGameObject()
    {
        base.DropGameObject();
        mbLinkedToGo = false;
    }

    public override void OnDelete()
    {
        if (WorldScript.mbIsServer)
            if (mStoredLenses != null)
                if (mStoredLenses.GetAmount() > 0)
                    ItemManager.instance.DropItem(mStoredLenses, mnX, mnY, mnZ, Vector3.zero);
    }

    //model selection
    public override void SpawnGameObject()
    {
        base.SpawnGameObject();
    }

    public override void LowFrequencyUpdate()
    {
        ++mUpdates;
        if(mUpdates%20 == 0 && mDebug)
        {
            string txt = "[Auto Lens Swapper][DEBUG] Yes, LFUT is running.";
            txt += "\nmIssue: " + mIssue.ToString();
            txt += "\nmStatus: " + mStatus.ToString();
            Debug.LogWarning(txt);
        }
        float seconds = LowFrequencyThread.mrPreviousUpdateTimeStep;
        UpdatePlayerDistanceInfo(); //cull inside rooms.


        switch (mStatus)
        {
            case eStatuses.Stopped:
                checkReady();
                break;
            case eStatuses.Running:
                if (mIssue == eIssues.Ready) run();
                else checkReady();
                break;
            case eStatuses.Done:
                break;
            default:
                break;
        }
    }

    public override void UnityUpdate()
    {
        /*
        if (!mbLinkedToGo)
        {
            GameObject lObj = SpawnableObjectManagerScript.instance.maSpawnableObjects[(int)SpawnableObjectEnum.PowerStorageBlock].transform.Search("HoloCube").gameObject;
            HoloCubePreview = GameObject.Instantiate(lObj, mWrapper.mGameObjectList[0].gameObject.transform.position + new Vector3(0.0f, 0.75f, 0.0f), Quaternion.identity);
            mbLinkedToGo = true;
        }
        */
    }

    public override string GetPopupText()
    {
        int segmentCount = WorldScript.instance.mSegmentUpdater.updateList.Count;
        //Header
        string retText = "Auto Lens Swapper";
        retText += "   Power: " + Mathf.Round(mrCurrentPower).ToString() + " / " + Mathf.Round(mrMaxPower).ToString();
        retText += "\neStatus: " + mStatus.ToString();
        retText += "   eIssue: " + mIssue.ToString();
        if (mTargetLens != null)
        {
            retText += "\nNew lens: " + mTargetLens.GetDisplayString();
            retText += "   Stored: " + (mnStorageMax - getStorageAvailable()).ToString() + " / " + mnStorageMax.ToString();
        }
        else retText += "\nNew lens: null - Press T to insert & set!";
        retText += "\n------";
        //Body
        switch (mStatus)
        {
            case eStatuses.Stopped:

                if ( mIssue != eIssues.Ready) retText += GetIssueText(mIssue);
                else
                {
                    /*
                    if (mbOnlySwap) retText += "\nMode: swap only (Shift + T)";
                    else retText += "\nMode: swap & insert (Shift + T)";
                    if (mbTrashOld) retText += "\nOld lenses: destroy (Shift + Q)";
                    else retText += "\nOld lenses: output to adjacent machine (Shift + Q)";
                    */
                    retText += "\n\nPress E to start running!";
                    retText += "\nPress Q to remove lenses and reset machine at any time.";
                    // ^^^ replace with dynamic text for "E"

                    /*
                    if (Input.GetButtonDown("Store") && Input.GetKeyDown(KeyCode.LeftShift))// && UIManager.AllowInteracting)
                        mbOnlySwap = !mbOnlySwap;
                    else if (Input.GetButtonDown("Extract") && Input.GetKeyDown(KeyCode.LeftShift))// && UIManager.AllowInteracting)
                        mbTrashOld = !mbTrashOld;
                    */
                    if (Input.GetButtonDown("Interact") && !Input.GetKeyDown(KeyCode.LeftShift))// && UIManager.AllowInteracting)
                        mStatus = eStatuses.Running;
                }
                break;

            case eStatuses.Running:
                //power, stored lenses, percent_progress, ID, and max
                if (mIssue != eIssues.Ready) retText += GetIssueText(mIssue);
                else
                {
                    retText += "\nSegment: " + mnSegmentID.ToString() + " / " + segmentCount.ToString() + " (" + (Mathf.Round((float)mnSegmentID / (float)segmentCount * 100f)).ToString() + "%)";
                    retText += "\nLPTs checked: " + mnTrackLPTs.ToString();
                    retText += "\nLens swaps: " + mnTrackSwaps.ToString();
                }
                    retText += "\nPress Q to remove lenses and reset machine.";
                break;

            case eStatuses.Done:
                //finished running over ID segments, swapping index? lenses
                if (mbHaltedEarly) retText += "\nHalted early!";
                else               retText += "\nComplete!";
                retText += "   Segments checked: " + mnSegmentID.ToString();
                retText += "   LPTs checked: " + mnTrackLPTs.ToString();
                retText += "   Lens swaps: " + mnTrackSwaps.ToString();
                retText += "\nPress Q to start over!";
                // ^^^ replace with dynamic text for "Q"
                break;


            default:
                break;
        }

        if (Input.GetButtonDown("Extract") && !Input.GetKey(KeyCode.LeftShift) && UIManager.AllowInteracting) PlayerExtractRequest();
        if (Input.GetButtonDown("Store") && !Input.GetKey(KeyCode.LeftShift) && UIManager.AllowInteracting) PlayerStoreRequest();

        return retText;
    }

    private void PlayerExtractRequest()
    {
        Player player = WorldScript.mLocalPlayer;
        int lensCount = 0;

        if (player == null) return;

        if(mStoredLenses != null) lensCount = mStoredLenses.GetAmount();
        if (lensCount > 0)
        {
            Debug.Log("[Auto Lens Swapper] Removing " + lensCount + " lenses from machine, placing into " + player.mUserName);
            if (!player.mInventory.AddItem(mStoredLenses))
            {
                ItemManager.instance.DropItem(mStoredLenses, player.mnWorldX, player.mnWorldY, player.mnWorldZ, Vector3.zero);
                Debug.Log("[Auto Lens Swapper] Player's inventory did not accept lenses. Dropping at player's feet.");
            }
        }
        mStoredLenses = null;
        mTargetLens = null;

        if (mStatus == eStatuses.Running)
        {
            mbHaltedEarly = true;
            mStatus = eStatuses.Done;
        }
        else if (mStatus == eStatuses.Done)
        {
            mnTrackLPTs = 0;
            mnTrackSwaps = 0;
            mnSegmentEIndex1 = 0;
            mnSegmentEIndex2 = 0;
            mnSegmentID = 0;
            mbHaltedEarly = false;
            mStatus = eStatuses.Stopped;
        }


        MarkDirtyDelayed();
        RequestImmediateNetworkUpdate();
        player.mInventory.VerifySuitUpgrades(); //Shouldn't be needed, but lets be safe.
        SurvivalHotBarManager.MarkAsDirty();
        SurvivalHotBarManager.MarkContentDirty();
        UIManager.ForceNGUIUpdate = 0.1f;
        RequestImmediateNetworkUpdate();
        AudioHUDManager.instance.OrePickup();
    }

    private void PlayerStoreRequest()
    {
        int lnAvailable = 0;
        ItemBase itemToStore = UIManager.instance.GetCurrentHotBarItemOrCubeAsItem(out lnAvailable, true);
        Player player = WorldScript.mLocalPlayer;

        if (player == null) return;
        if (lnAvailable < 0) return;
        if (itemToStore == null) return;
        if (!isValidLensID(itemToStore.mnItemID))
        {
            Debug.Log("[Auto Lens Swapper] Player " + player.mUserName + " tried inserting a non-lens, but was denied!");
            Debug.Log("[Auto Lens Swapper] itemID: " + itemToStore.mnItemID + " itemName: " + itemToStore.GetName());
            return;
        }
        if (mTargetLens != null)
        {
            if (itemToStore.mnItemID != mTargetLens.mnItemID && getStorageAvailable() < mnStorageMax)
            {
                Debug.Log("[Auto Lens Swapper][info] Player " + player.mUserName + " tried inserting a differing lens than what was still in the machine!");
                return;
            }
        }
        
        //Easy case, only setting target, not modifying storage.
        //(User didn't have any lenses on them, but they had the target in their hotbar)
        if (lnAvailable == 0)
        {
            Debug.Log("[Auto Lens Swapper][info] Player " + player.mUserName + " set target (new) lens without depositing any.");
            mTargetLens = ItemManager.SpawnItem(itemToStore.mnItemID);
            if (player.mbIsLocalPlayer)
            {
                Color lCol = Color.red;
                FloatingCombatTextManager.instance.QueueText(mnX, mnY + 1L, mnZ, 0.75f, string.Format("Lens Swapper set to: \n", (object)player.GetItemName(itemToStore)), lCol, 1.5f, 64f);
            }
            return;
        }

        int amount = lnAvailable;
        int storageAvailable = getStorageAvailable();
        int maxStackSize = ItemManager.GetMaxStackSize(itemToStore);

        //Determine amount that can be transfered.
        if (itemToStore.mnItemID == -1) return;
        if (amount > 10 && Input.GetKey(KeyCode.LeftShift))
            amount = 10;
        if (amount > 1 && Input.GetKey(KeyCode.LeftControl))
            amount = 1;
        if (amount > getStorageAvailable())
            amount = getStorageAvailable();
        if (amount > ItemManager.GetMaxStackSize(itemToStore))
            amount = ItemManager.GetMaxStackSize(itemToStore);
        ItemManager.SetItemCount(itemToStore, amount);
        
        if (!player.mInventory.RemoveItemByExample(itemToStore, true))
        {
            Debug.Log("[Auto Lens Swapper][info] Player " + player.mUserName + " doesnt have " + itemToStore.GetName());
            return;
        }
        mTargetLens = ItemManager.SpawnItem(itemToStore.mnItemID);
        if (mStoredLenses == null || mStoredLenses.mnItemID != mTargetLens.mnItemID)
        {
            mStoredLenses = ItemManager.SpawnItem(itemToStore.mnItemID);
            AddLenses(-1);// SpawnItem starts with the amount as 1.
        }
        AddLenses(amount);
        player.mInventory.VerifySuitUpgrades(); //Shouldn't be needed, but lets be safe.
        Debug.Log("[Auto Lens Swapper][info] Player " + player.mUserName + " stored lenses manually!");

        //render in-world pop-up text.
        if (player.mbIsLocalPlayer)
        {
            Color lCol = Color.cyan;
            FloatingCombatTextManager.instance.QueueText(mnX, mnY + 1L, mnZ, 0.75f, string.Format(PersistentSettings.GetString("Stored_X"), (object)player.GetItemName(itemToStore)), lCol, 1.5f, 64f);
        }
        //else
            //StorageHopperWindowNew.networkRedraw = true;
        player.mInventory.VerifySuitUpgrades();
        /*
        if (!WorldScript.mbIsServer)
            NetworkManager.instance.SendInterfaceCommand(nameof(StorageHopperWindowNew), nameof(StoreItems), (string)null, itemToStore, (SegmentEntity)hopper, 0.0f);
        */
        // ^^^ Reimplement?

        MarkDirtyDelayed();
        RequestImmediateNetworkUpdate();
        UIManager.ForceNGUIUpdate = 0.1f;
        AudioHUDManager.instance.HUDOut();
        SurvivalHotBarManager.MarkAsDirty();
        SurvivalHotBarManager.MarkContentDirty();
    }

    private bool checkReady()
    {
        if (mUpdates % 20 == 0 && mDebug)
        {
            string txt = "[Auto Lens Swapper][DEBUG] checkReady called.";
            txt += "\nmIssue: " + mIssue.ToString();
            Debug.LogWarning(txt);
        }

        if (mrCurrentPower < mrPowerPerSwap)
        {
            mIssue = eIssues.Power;
            if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: Power:" + mrCurrentPower + " - " + mrMaxPower);
            return false;
        }
        else if (mTargetLens == null)
        {
            mIssue = eIssues.SetLens;
            if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: setlens, null");
            return false;
        }
        else if (!isValidLensID(mTargetLens.mnItemID))
        {
            mIssue = eIssues.SetLens;
            if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: setlens invalid" + isValidLensID(mTargetLens.mnItemID));
            return false;
        }
        else if (mStoredLenses == null)
        {
            mIssue = eIssues.Input;
            if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: stored lenses null" + mStoredLenses.GetDisplayString());
            return false;
        }
        else if (mStoredLenses.GetAmount() <= 0)
        {
            if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: stored lenses 0");
            if (!TakeLensSurrounding())
            {
                mIssue = eIssues.Input;
                if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: Couldn't pull any lenses!");
                return false;
            }
        }
        else if (mIssue == eIssues.Output)
        {
            if (mUpdates % 20 == 0 && mDebug) Debug.LogWarning("[Auto Lens Swapper][DEBUG] checkReady Issue: Couldn't drop-off lenses!");
            if (mLastLPT == null) Debug.LogWarning("[Auto Lens Swapper][info] eIssue was output, but then mLastLPT was null! Assuming lpt was deleted and moving on.");
            else if (!runSwapLens(mLastLPT)) return false;
            mIssue = eIssues.Ready;
            RequestImmediateNetworkUpdate();
        }
        else if (mIssue != eIssues.Ready)
        {
            mIssue = eIssues.Ready;
            RequestImmediateNetworkUpdate();
        }
        else mIssue = eIssues.Ready;
        return true;
    }

    //Run, the main swapping function.
    //This function loops through all of the game segments, using small sprints.
    //Sprints will either end from passing through too many segments, or making too many changes, or encountering an issue.
    private void run()
    {
        if (mIssue != eIssues.Ready) return;
        if (mStatus != eStatuses.Running) return;
        
        int sprintSegment = 0;
        int swapsAtSprintStart = mnTrackSwaps;
        int segmentCount = WorldScript.instance.mSegmentUpdater.updateList.Count;

        //Limited sprint, to prevent eating CPU time.
        for (sprintSegment = 0; sprintSegment < 256 && mnSegmentID < segmentCount; ++sprintSegment)
        {
            runLoopSegEntities(mnSegmentID);
            if (mIssue != eIssues.Ready) break;
            ++mnSegmentID;
            //limit how many changes we can do in a LFUT-tick.
            //I suspect this will be more limiting than 256
            if (mnTrackSwaps - swapsAtSprintStart > 4) break;
        }

        if (mnSegmentID >= segmentCount)
        {
            Debug.LogWarning("[Auto Lens Swapper][info] Completed a pass!");
            mStatus = eStatuses.Done;
            mbHaltedEarly = false;
            RequestImmediateNetworkUpdate();
            return;
        }

        //MarkDirtyDelayed();
        return;
    }

    //After finding a valid segment, this function will loop through the 2D array of entities.
    //The function will first try to resume from the stored index, incase it was previously paused by an issue.
    //In this and deeper functions, only issues cause pausing.
    private void runLoopSegEntities(int segID)
    {
        Segment segment = WorldScript.instance.mSegmentUpdater.updateList[segID];

        if (segment == null || !segment.mbInitialGenerationComplete || segment.mEntities == null)
            return;
        
        //No-issues? Hunt for the next entity. Entities are stored in a 2D array.
        for (; mnSegmentEIndex1 < segment.mEntities.Length; ++mnSegmentEIndex1)
        {
            if(segment.mEntities[mnSegmentEIndex1] != null && segment.mEntities[mnSegmentEIndex1].Count > 0)
            {
                for(; mnSegmentEIndex2 < segment.mEntities[mnSegmentEIndex1].Count; ++mnSegmentEIndex2)
                {
                    runCheckEntity(segment.mEntities[mnSegmentEIndex1][mnSegmentEIndex2]);
                    if (mIssue != eIssues.Ready) break;
                }
                if (mIssue != eIssues.Ready) break; //break all the way out, do not reset indecies.
                //on succesfull loop, reset index.
                mnSegmentEIndex2 = 0;
            }
        }
        if (mIssue != eIssues.Ready) return; //break all the way out, do not reset indecies.
        //on succesfull loop, reset index.
        mnSegmentEIndex1 = 0;
        return;
    }

    //Small function to check the type of an entity.
    //Prevents code repetition.
    private void runCheckEntity(SegmentEntity entity)
    {
        if (entity != null)
            if (entity.mType == eSegmentEntity.LaserPowerTransmitter)
                if (checkReady()) //Check now, incase we ran out of power / lenses
                    runSwapLens((LaserPowerTransmitter)entity);
        return;
    }

    //Executing the namesake of the mod.
    //Function will confirm that all needs are met for swapping a lens.
    //Proper care needs to be taken here to never delete an "old lens", as the ALS has no 
        //internal storage for oldLenses. 
    //Must not have any mIssue or checkReady checks in this funtion; lest it become recursive/unreachable.
    private bool runSwapLens(LaserPowerTransmitter lpt)
    {
        if (lpt == null) return false;
        
        ItemBase oldLens = lpt.GetLens();
        bool hasLens = (oldLens != null);

        if (mbOnlySwap && !hasLens)
        {
            ++mnTrackLPTs;
            return false;
        }
        
        if (hasLens)
        {
            if (oldLens.mnItemID == mStoredLenses.mnItemID)
            {
                ++mnTrackLPTs;
                return false;
            }
            else
            {
                if (!CommunityUtil.GiveToSurrounding((MachineEntity)this, oldLens))
                {
                    Debug.LogWarning("[Auto Lens Swapper][issue] Could not find a surrounding machine to drop an old lens into! Lens was not removed.");
                    mIssue = eIssues.Output;
                    mLastLPT = lpt;
                    return false;
                }
            }
        }

        lpt.SwapLens(mTargetLens);
        AddLenses(-1);
        mrCurrentPower -= mrPowerPerSwap;
        ++mnTrackLPTs;
        ++mnTrackSwaps;
        MarkDirtyDelayed();
        return true;
    }

    public override bool ShouldSave() => true;
    public override void Write(BinaryWriter writer)
    {
        writer.Write(mrCurrentPower);
        ItemFile.SerialiseItem(mTargetLens, writer);
        ItemFile.SerialiseItem(mStoredLenses, writer);
        //Concious decision to not store progress/state.
        //Should be a "fast" machine anyways.
        //This will ensure no material/energy is lost.
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
        mrCurrentPower = reader.ReadSingle();
        mTargetLens = ItemFile.DeserialiseItem(reader);
        mStoredLenses = ItemFile.DeserialiseItem(reader);
    }

    public override bool ShouldNetworkUpdate() => true;
    public override void WriteNetworkUpdate(BinaryWriter writer)
    {
        writer.Write(NetworkServerIO.FloatToByte(mrCurrentPower));
        ItemFile.SerialiseItem(mTargetLens, writer);
        ItemFile.SerialiseItem(mStoredLenses, writer);

        writer.Write((byte) mIssue);
        writer.Write((byte)mStatus);
        writer.Write(mbHaltedEarly);
        writer.Write(mbOnlySwap);
        writer.Write(mbTrashOld);
        writer.Write(mnTrackLPTs);
        writer.Write(mnTrackSwaps);
    }

    public override void ReadNetworkUpdate(BinaryReader reader)
    {
        mrCurrentPower = NetworkServerIO.ByteToFloat(reader.ReadByte());
        mTargetLens = ItemFile.DeserialiseItem(reader);
        mStoredLenses = ItemFile.DeserialiseItem(reader);

        mIssue = (eIssues)reader.ReadByte();
        mStatus = (eStatuses)reader.ReadByte();
        mbHaltedEarly = reader.ReadBoolean();
        mbOnlySwap = reader.ReadBoolean();
        mbTrashOld = reader.ReadBoolean();
        mnTrackLPTs = reader.ReadInt16();
        mnTrackSwaps = reader.ReadInt16();
    }

    private string GetIssueText(eIssues issue)
    {
        string txt = "";
        string seeHandbook = "\n\nCheck handbook? (H)\n" + PersistentSettings.GetString("See_the_handbook_for_details");

        switch (issue)
        {
            case eIssues.Power:
                txt += "\nIssue: Low Power!" + seeHandbook;
                break;
            case eIssues.Input:
                txt += "\nIssue: Cannot find lenses!";
                txt += "\nAre hoppers locked?";
                txt += "\nPress T to insert lenses!" + seeHandbook;
                // ^^^ replace with dynamic text for "T"
                break;
            case eIssues.Output:
                txt += "\nIssue: Attached storage missing or full!" + seeHandbook;
                break;
            case eIssues.SetLens:
                txt += "\nIssue: (T)ell the machine what lens to use!";
                // ^^^ replace with dynamic text for "T"
                break;
            case eIssues.Ready:
                break;
            default:
                break;
        }
        return txt;
    }

    public int getStorageAvailable()
    {
        if (mStoredLenses == null) return mnStorageMax;
        return mnStorageMax - mStoredLenses.GetAmount();
    }

    //Little helpers
    public bool isValidLensID(int itemID) => (minLensID <= itemID && itemID <= maxLensID);
    private void AddLenses(int amount) => mStoredLenses.SetAmount(mStoredLenses.GetAmount() + amount);

    //PowerInterface Methods
    public bool WantsPowerFromEntity(SegmentEntity entity) => true;
    public float GetMaxPower() => mrMaxPower;
    public float GetMaximumDeliveryRate() => mrMaxTransferRate;
    public float GetRemainingPowerCapacity() => mrMaxPower - mrCurrentPower;
    public bool DeliverPower(float amount)
    {
        if ((double)amount > (double)this.GetRemainingPowerCapacity())
            return false;
        mrCurrentPower += amount;
        MarkDirtyDelayed();
        return true;
    }

    public bool TryDeliverItem(StorageUserInterface sourceEntity, ItemBase item, ushort cubeType, ushort cubeValue, bool sendImmediateNetworkUpdate)
    {
        if (!isValidLensID(item.mnItemID)) return false; //Not a lens!
        if (item.GetAmount() <= 0) return false;
        if (item.mnItemID != mTargetLens.mnItemID) return false; //Must be correct type!
        if (item.GetAmount() > getStorageAvailable()) return false;
        AddLenses(item.GetAmount());
        MarkDirtyDelayed();
        return true;
    }

    private bool TakeLensSurrounding()
    {
        ItemBase taken = CommunityUtil.TakeFromSurrounding((MachineEntity)this, mTargetLens);
        if (taken == null) return false;
        if (taken.GetAmount() <= 0) return false;
        if (taken.GetAmount() > getStorageAvailable()) Debug.Log("[Auto Lens Swapper][info] 'Take Surrounding' took more than max_storage!\nNothing lost, but not intended!");
        AddLenses(taken.GetAmount());
        MarkDirtyDelayed();
        return true;
    }
}