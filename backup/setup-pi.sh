if [[ -f "./backup-srv-to-windows.env" ]]; then
    source "./backup-srv-to-windows.env"
else
    echo "Environment file ./backup-srv-to-windows.env not found. Please create it with WIN_USER and WIN_HOST."
    exit 1
fi

ssh-keygen -t ed25519 -C "pi-to-windows-backup"

echo "Public key generated. Please copy the following key to your Windows server's authorized_keys file:"
cat ~/.ssh/id_ed25519.pub
echo ""
echo "Instructions:"
echo "1. Log in to your Windows server."
echo "2. Open (or create) the file: C:\Users\<YourUsername>\.ssh\authorized_keys"
echo "3. Paste the above public key into the file and save."
echo "4. Ensure the permissions are correct."
