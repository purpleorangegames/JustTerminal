# why: what a colonist is thinking, worst first, like why Tasca

if args() = 0
  print dim usage: why <name>
  stop
end
if not exists("/pawns/" + arg(1))
  print bad no colonist called {arg(1)}
  stop
end
p = path("/pawns/" + arg(1))

out bright {p/name}
if p/mood = none
  print dim   no mood
  stop
end
print dim   mood {round(p/mood)}
for t in p/thoughts
  if t/mood < 0
    out bad {padl(t/mood, 6)}
  else
    out good {padl("+" + t/mood, 6)}
  end
  print   {t/name}
end
