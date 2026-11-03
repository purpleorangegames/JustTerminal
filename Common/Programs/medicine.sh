# medicine: show or set medical care, an edit mode command
# medicine all herbal, medicine Tasca best, medicine @miners

if args() = 0
  print dim usage: medicine <who> [none|nomeds|herbal|industrial|best]
  stop
end

if args() = 1
  for p in pawns(arg(1))
    out bright {pad(p/name, 14)}
    print {path("/care/" + p/medicine)}
  end
  stop
end

care = lower(arg(2))
if not exists("/care/" + care)
  print bad care is one of none, nomeds, herbal, industrial, best
  stop
end

changed = ""
for p in pawns(arg(1))
  set p/medicine = care
  if changed != ""
    changed = changed + ", "
  end
  changed = changed + p/name
end
print good {changed}: {path("/care/" + care)}
