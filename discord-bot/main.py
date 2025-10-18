import csv
from unicodedata import category
import discord
import os
import psutil
from dotenv import load_dotenv
from discord import app_commands
from datetime import datetime as dt
from discord.ui import View, Select

load_dotenv()
DISCORD_TOKEN = os.getenv("DISCORD_TOKEN")
USER_ID = int(os.getenv("USER_ID"))
CATEGORIES_FILE = "/srv/actual-inserter/categories.csv"

intents = discord.Intents.default()
intents.message_content = True

client = discord.Client(intents=intents)
tree = app_commands.CommandTree(client)
categories = []

@client.event
async def on_ready():
    global categories
    categories = read_categories()
    print(f"✅ Logged in as {client.user}")
    # Sync commands with Discord (only needs to happen once per startup)
    try:
        synced = await tree.sync()
        print(f"🔄 Synced {len(synced)} command(s)")
    except Exception as e:
        print(f"⚠️ Sync error: {e}")

@client.event
async def on_message(message):
    if message.author == client.user:
        return

    if message.content.lower() == "ping":
        await message.channel.send("pong 🏓")

@tree.command(name="status", description="Get Raspberry Pi system status")
async def status(interaction: discord.Interaction):
    # Gather basic system stats
    cpu = psutil.cpu_percent(interval=0.5)
    mem = psutil.virtual_memory().percent
    temp = 0.0
    try:
        # On Raspberry Pi: /sys/class/thermal/thermal_zone0/temp
        with open("/sys/class/thermal/thermal_zone0/temp") as f:
            temp = int(f.read().strip()) / 1000
    except FileNotFoundError:
        pass

    message = (
        f"🖥️ **System Status**\n"
        f"• CPU Usage: `{cpu}%`\n"
        f"• Memory Usage: `{mem}%`\n"
        f"• Temperature: `{temp:.1f} °C`\n"
    )

    await interaction.response.send_message(message)


@tree.command(name="expense", description="Add expense: /expense <category> <note> <amount>")
async def expense(interaction: discord.Interaction, category: str, note: str, amount: float):
    global categories
    today = dt.now().strftime("%Y-%m-%d")
    # Case-insensitive category check
    categories_lower = [c.lower() for c in categories]
    if category.lower() not in categories_lower:
        cats = ', '.join(categories)
        await interaction.response.send_message(
            f"❌ Category '{category}' not found. Available categories: {cats}", ephemeral=True
        )
        return
    # Use the original case from categories for writing
    matched_category = categories[categories_lower.index(category.lower())]

    # Write to CSV
    csv_file = f"/srv/actual-inserter/transaction-{today}.csv"
    file_exists = os.path.isfile(csv_file)
    with open(csv_file, 'a', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        if not file_exists:
            writer.writerow(['date', 'amount', 'category', 'notes'])
        writer.writerow([today, amount, matched_category, note])

    await interaction.response.send_message(
        f"Added expense: category=`{matched_category}` note=`{note}` amount=`{amount}` on {today}", ephemeral=True
    )

def read_categories():
    """Read categories from CSV and return a list of (name)."""
    categories = []
    with open(CATEGORIES_FILE, newline='', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            categories.append(row["name"])
    return categories

client.run(DISCORD_TOKEN)
