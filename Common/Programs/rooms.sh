# rooms: the colony's rooms with temperature, cleanliness and impressiveness

out dim {pad("ROOM", 32)}
out dim {padl("TEMP", 7)}
out dim {padl("CLEAN", 8)}
print dim {padl("IMPRESSIVE", 12)}
for r in /rooms
  out {pad(r/name, 32)}
  out {padl(r/temp + "C", 7)}
  if r/cleanliness < 0
    out warn {padl(round(r/cleanliness, 2), 8)}
  else
    out {padl(round(r/cleanliness, 2), 8)}
  end
  print {padl(round(r/impressiveness), 12)}
end
