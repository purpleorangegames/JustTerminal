# fetch: colony info next to a little picture

c = /colony
pr = prompt()
blank = repeat(" ", 24)

out accent {pad("      *        .   ", 24)}
out accent {before(pr, "@")}
out dim @
print accent {after(pr, "@")}
out accent {pad("   .     .-~~~-.   ", 24)}
print dim {repeat("─", len(pr))}
out accent {pad("       /'       '\ ", 24)}
out bright colony:
print  {c/name}
out accent {pad("  ~===|===========|=~", 24)}
out bright storyteller:
print  {c/storyteller}, {c/difficulty}
out accent {pad("       \.       ./  ", 24)}
out bright day:
print  {c/day}
out accent {pad("  *      '-~~~-'   .", 24)}
out bright colonists:
print  {c/colonists}
out accent {pad("    .         *     ", 24)}
out bright prisoners:
print  {c/prisoners}
out {blank}
out bright slaves:
print  {c/slaves}
out {blank}
out bright animals:
print  {c/animals}
out {blank}
out bright wealth:
print  {num(c/wealth)}
out {blank}
out bright biome:
print  {c/biome}
out {blank}
out bright maps:
print  {c/maps}
if c/happiest != none
  out {blank}
  out bright happiest:
  print  {c/happiest}
end

# the screen's colors
print
out {blank}
out dim ███
out ███
out bright ███
out good ███
out warn ███
out bad ███
print accent ███
