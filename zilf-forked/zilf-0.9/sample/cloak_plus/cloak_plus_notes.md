Cloak Plus
==========

This poor, extended Cloak of Darkness example is now stuffed with extra things
that in no way provide an interesting gaming experience, but do provide
demonstrations of several aspects of the ZILF libary (the world model and
special routines in parser.zil).

Intended for testing the ZILF library, it will serve as a demo of ZILF
features/coding and eventually be replaced with a better demo.

SAVE, RESTORE, RESTART, AGAIN, WAIT and UNDO should all function as expected.

If you find any bugs, please report them at the [ZILF issue tracker].

[ZILF issue tracker]: https://vaporware.atlassian.net/projects/ZILF/issues


Things to do in Cloak Plus
--------------------------

### The Foyer ###

- Examining the cube should cause 10 game turns to pass.

- Examining the painting or card should randomly display a different
  description each time, not repeating until all possible descriptions have
  been shown once (6 for the painting, 3 for the card).

- Reading the painting should pick from three different signature possibilities
  to display, in a 'totally' random fashion - ie, it doesn't guarantee all
  other possibilities have been shown before repeating a particular
  possibility.

- Eating the apple should kill you, taking you to a "quit, restore, undo or
  restart?" query.

- IQUEUE event tests:

    - A "You looked at grime 1 turn ago" message event should fire 1 turn after
      any EXAMINE GRIME action.

    - A "You looked at apple..." message should fire 2 turns after any EXAMINE
      APPLE action.

    - The Foyer room's own routine should report if the above apple event is
      going fire (saying "The Foyer routine detects..." - a test of the RUNNING?
      (as in, is this event running?) routine.

    - Examining the table should cause an event to fire every turn, until you
      examine the HOOK in the Cloakroom, which should dequeue it.  Note 'every
      turn' events do not fire during meta-actions like INVENTORY.

### The Cloakroom ###

- You should have to remove the Cloak before traveling west to Hallway to Study.

### Hallway to Study ###

- Event describing spider should interrupt a wait cycle, ie full 4 turns won't
  go by. Example of RTRUE at end of IQUEUE event.

- Examining the SIGN should reveal both its description and its text, reading
  it should only reveal its text.

### The Study ###

- The random event descriptions here (of mouse and scratching sound) should
  *not* interrupt a wait cycle. (There are examples of using RFALSE at end of
  IQUEUE event.)

- Has many containers and surfaces to test PUT IN and PUT ON with: jar is an
  always open container, wallet is a takeable container with very limited
  capacity which can be opened and closed, safe is a non-takeable container
  that can be opened and closed.  Tray and stand are surfaces.  Crate is an
  always-closed container.  The case is a transparent unopenable container
  holding a muffin that can be seen but not taken.

- You can SWITCH/TURN ON and SWITCH/TURN OFF and FLIP the LIGHTSWITCH to
  control whether the Closet is lit or not.  The flashlight is a device that
  you can SWITCH/TURN ON and SWITCH/TURN OFF, provides light when on.  The
  sphere is a transparent unopenable container that holds a firefly which
  provides light.

- You should have to hold the book to be able to read it.

### General ###

- Pronouns:

    - IT should refer to the last direct object you used in a command.

    - HE and SHE can be used to refer to Bentley and Stella after you've used
      their names in commands at least once.

    - THEM can be used to reference the grapes (the grapes have PLURALBIT).

- The CEILING is a global object that should be referenceable in every room.

- DARKNESS is an abstract (GENERIC in ZIL terms) object that you can THINK
  ABOUT.

- The RUG is a LOCAL-GLOBAL (scenery in multiple rooms) object that is in both
  the Foyer and the Bar.
