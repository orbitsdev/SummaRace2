import asyncio, json, glob, os, edge_tts
VOICE="en-US-AvaMultilingualNeural"; RATE="-15%"; OUT="VoiceSamples/ava"  # staging, OUTSIDE Assets/ (git-excluded); copy over the existing clips so .meta survives
jobs=[]
for f in sorted(glob.glob("Assets/_Game/Resources/Stories/s*.json")):
    s=json.load(open(f,encoding="utf-8"))
    for i,p in enumerate(s["pages"]):
        name=os.path.basename(p["narration"])
        jobs.append((p["text"], f"{OUT}/{name}.mp3", "Stories/Narration/"+name+".mp3"))
vo={
 "vo_arrange_title":"Great running! Put the story parts in order!",
 "vo_name_hello":"Hi! What's your name?",
 "vo_arrange_how":"Tap a story part, then tap where it goes.",
 "vo_summary_title":"Write your summary!",
 "vo_summary_hint":"Example: Somebody wanted blank, but blank, so blank, then blank.",
 "vo_race_briefing":"Collect the 5 story parts in order. Tap the answer you want!",
 "vo_race_briefing_patrol":"Miss one? It comes back! The patrol chases you, but never catches you!",
 "vo_tip_somebody":"SOMEBODY is who the story is about.",
 "vo_tip_wanted":"WANTED is what the character wanted.",
 "vo_tip_but":"BUT is the problem the character had.",
 "vo_tip_so":"SO is what the character did about it.",
 "vo_tip_then":"THEN is how the story ended.",
}
for k,t in vo.items(): jobs.append((t, f"{OUT}/{k}.mp3", "Audio/"+k+".mp3"))
sem=asyncio.Semaphore(6); fails=[]
async def one(text,out):
    async with sem:
        for attempt in range(4):
            try:
                await edge_tts.Communicate(text, VOICE, rate=RATE).save(out)
                if os.path.getsize(out)>1000: return
            except Exception as e: err=e
            await asyncio.sleep(2)
        fails.append(out)
async def main(): await asyncio.gather(*[one(t,o) for t,o,_ in jobs])
asyncio.run(main())
json.dump([[o,d] for _,o,d in jobs], open(f"{OUT}/map.json","w"))
print("jobs",len(jobs),"fails",len(fails),fails[:5])
