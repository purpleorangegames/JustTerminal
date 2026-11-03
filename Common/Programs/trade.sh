# trade: the traders on the map, trade 1 shows what the first one carries

if count(/trade) = 0
  print dim no traders on the map
  stop
end

if args() = 0
  for t in /trade
    out bright {pad(name(t), 4)}
    out {pad(t/name, 16)}
    out dim {pad(t/kind, 24)}
    print dim {t/silver} silver
  end
  stop
end

n = arg(1)
if not exists("/trade/" + n)
  print bad no trader {arg(1)}
  stop
end
t = path("/trade/" + n)
print bright {t/name}, {t/kind}
for g in t/goods
  out {pad(g/name, 32)}
  out {padl(g/count, 6)}
  print dim {padl(round(g/value), 7)} each
end
