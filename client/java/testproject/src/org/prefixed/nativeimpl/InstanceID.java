package org.prefixed.nativeimpl;

public class InstanceID {
    public int id;
    public boolean isClientID; // otherwise Server ID
    public InstanceID(int id, boolean isClientID) {
        this.id = id;
        this.isClientID = isClientID;
    }
}
