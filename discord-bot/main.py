import csv
import discord
import os
import psutil
from dotenv import load_dotenv
from discord import app_commands
from datetime import datetime as dt

load_dotenv()
DISCORD_TOKEN = os.getenv("DISCORD_TOKEN")
USER_ID = int(os.getenv("USER_ID"))

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

client.run(DISCORD_TOKEN)
